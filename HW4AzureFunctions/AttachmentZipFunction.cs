using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HW4AzureFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;

namespace HW4AzureFunctions
{
    /// <summary>
    /// Azure Function triggered by messages in the <c>attachment-zip-requests</c> queue.
    /// For each message the function:
    /// <list type="number">
    ///   <item>Deserialises the <see cref="ZipRequest"/> payload.</item>
    ///   <item>Lists all blobs in the attachment container named with the note ID.</item>
    ///   <item>Creates an in-memory zip archive containing all attachment blobs.</item>
    ///   <item>Uploads the archive to the <c>{noteId}-zip</c> container as the blob named by <c>ZipFileId</c>.</item>
    /// </list>
    /// If processing fails the message is retried up to <c>maxDequeueCount</c> times
    /// (configured in <c>host.json</c>) before being moved to the poison queue
    /// <c>attachment-zip-requests-poison</c>.
    /// </summary>
    public class AttachmentZipFunction
    {
        private readonly BlobStorageHelper _blobStorageHelper;
        private readonly ILogger<AttachmentZipFunction> _logger;
        private readonly string? _sqlConnectionString;

        /// <summary>
        /// Initialises a new instance of <see cref="AttachmentZipFunction"/>.
        /// </summary>
        public AttachmentZipFunction(BlobStorageHelper blobStorageHelper, ILogger<AttachmentZipFunction> logger, IConfiguration configuration)
        {
            _blobStorageHelper = blobStorageHelper;
            _logger = logger;
            _sqlConnectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// Entry point invoked by the Azure Functions runtime for each queue message.
        /// </summary>
        /// <param name="message">The raw JSON string dequeued from <c>attachment-zip-requests</c>.</param>
        /// <param name="context">The function execution context.</param>
        [Function("AttachmentZipFunction")]
        public async Task Run(
            [QueueTrigger("attachment-zip-requests", Connection = "AttachmentZipRequests")] string message,
            FunctionContext context)
        {
            _logger.LogInformation("AttachmentZipFunction triggered. Message: {Message}", message);

            ZipRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ZipRequest>(message,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialise queue message: {Message}", message);
                // Throwing causes the runtime to retry and eventually poison the message.
                throw;
            }

            if (request is null
                || !Guid.TryParse(request.NoteId, out _)
                || string.IsNullOrWhiteSpace(request.ZipFileId))
            {
                _logger.LogError(
                    "Invalid ZipRequest payload – NoteId={NoteId}, ZipFileId={ZipFileId}. Poisoning message.",
                    request?.NoteId, request?.ZipFileId);
                // Throwing causes retries; after maxDequeueCount it goes to the poison queue.
                throw new InvalidOperationException($"Invalid ZipRequest: NoteId={request?.NoteId}, ZipFileId={request?.ZipFileId}");
            }

            string noteId = request.NoteId.ToLower();
            string zipFileId = request.ZipFileId;
            string zipContainerName = $"{noteId}-zip";

            _logger.LogInformation(
                "Processing zip request – NoteId={NoteId}, ZipFileId={ZipFileId}, ZipContainer={ZipContainer}",
                noteId, zipFileId, zipContainerName);

            // Verify the note still exists in the database before doing any storage work
            if (!await NoteExistsInDatabaseAsync(request.NoteId))
            {
                _logger.LogWarning(
                    "Note {NoteId} does not exist in the database. Discarding stale zip request (ZipFileId={ZipFileId}).",
                    noteId, zipFileId);
                // Return without throwing – consume the message rather than retry/poison
                return;
            }

            BlobServiceClient blobServiceClient = _blobStorageHelper.Client;

            // Get the attachment container
            BlobContainerClient attachmentContainer = blobServiceClient.GetBlobContainerClient(noteId);

            if (!(await attachmentContainer.ExistsAsync()).Value)
            {
                _logger.LogWarning(
                    "Attachment container '{Container}' does not exist for note {NoteId}. Nothing to zip.",
                    noteId, noteId);
                return;
            }

            // Collect all blobs in the attachment container
            var blobNames = new List<string>();
            await foreach (BlobItem blobItem in attachmentContainer.GetBlobsAsync())
                blobNames.Add(blobItem.Name);

            if (blobNames.Count == 0)
            {
                _logger.LogWarning("No blobs found in container '{Container}' – zip will not be created.", noteId);
                return;
            }

            _logger.LogInformation("Found {Count} blob(s) in container '{Container}'", blobNames.Count, noteId);

            // Build zip archive in memory
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (string blobName in blobNames)
                {
                    BlobClient blobClient = attachmentContainer.GetBlobClient(blobName);
                    try
                    {
                        var downloadResult = await blobClient.DownloadContentAsync();
                        ZipArchiveEntry entry = archive.CreateEntry(blobName, CompressionLevel.Optimal);
                        using Stream entryStream = entry.Open();
                        await downloadResult.Value.Content.ToStream().CopyToAsync(entryStream);
                        _logger.LogDebug("Added '{BlobName}' to zip archive", blobName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add blob '{BlobName}' to zip archive", blobName);
                        throw;
                    }
                }
            }

            // Upload the zip archive to the {noteId}-zip container
            zipStream.Seek(0, SeekOrigin.Begin);

            BlobContainerClient zipContainer = blobServiceClient.GetBlobContainerClient(zipContainerName);
            await zipContainer.CreateIfNotExistsAsync();

            BlobClient zipBlobClient = zipContainer.GetBlobClient(zipFileId);
            await zipBlobClient.UploadAsync(zipStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/zip" }
            });

            _logger.LogInformation(
                "Successfully uploaded zip '{ZipFileId}' to container '{ZipContainer}' for note {NoteId}",
                zipFileId, zipContainerName, noteId);
        }

        /// <summary>
        /// Returns <c>true</c> if a note with the given ID exists in the SQL database.
        /// If the connection string is not configured the check is skipped (returns <c>true</c>).
        /// Any SQL error is re-thrown so the message is retried and eventually poisoned.
        /// </summary>
        private async Task<bool> NoteExistsInDatabaseAsync(string noteId)
        {
            if (string.IsNullOrWhiteSpace(_sqlConnectionString))
            {
                _logger.LogWarning("No SQL connection string configured – skipping DB existence check for note {NoteId}.", noteId);
                return true;
            }

            try
            {
                await using var connection = new SqlConnection(_sqlConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM Notes WHERE Id = @NoteId";
                command.Parameters.AddWithValue("@NoteId", Guid.Parse(noteId));
                int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database for NoteId {NoteId} – retrying message.", noteId);
                throw; // Let the runtime retry and eventually move to poison queue
            }
        }
    }
}
