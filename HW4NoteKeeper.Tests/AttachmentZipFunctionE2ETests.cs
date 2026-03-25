using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;

namespace HW4NoteKeeper.Tests
{
    /// <summary>
    /// End-to-end tests for the <c>AttachmentZipFunction</c> Azure Function.
    /// These tests place real messages on the <c>attachment-zip-requests</c> Azure Storage Queue,
    /// then assert that the function produces a zip blob in the expected <c>{noteId}-zip</c>
    /// container.  They require:
    /// <list type="bullet">
    ///   <item>The <c>func-HW4</c> Azure Function App to be deployed and running.</item>
    ///   <item>Blobs already uploaded to the <c>{noteId}</c> attachment container (or the test
    ///         creates them directly in storage).</item>
    ///   <item>Managed identity (<c>id-dbadmin</c>) granted Storage Queue Data Contributor +
    ///         Storage Blob Data Contributor on <c>st4hw3</c>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Never run these tests automatically – they require the Azure Function App
    /// to be deployed.  Run after publishing with <c>[Trait("Category","E2E")]</c> filter.
    /// </remarks>
    [Collection("Sequential")]
    [Trait("Category", "E2E")]
    public class AttachmentZipFunctionE2ETests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
    {
        private const string QueueName = "attachment-zip-requests";
        private const string StorageAccountName = "st4hw3";

        private readonly BlobServiceClient _blobServiceClient;
        private readonly QueueServiceClient _queueServiceClient;
        private readonly JsonSerializerOptions _jsonOptions;

        // containers / blobs to clean up after each test
        private readonly List<string> _containerNamesToDelete = new();

        public AttachmentZipFunctionE2ETests(WebApplicationFactory<Program> factory)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddUserSecrets<AttachmentZipFunctionE2ETests>()
                .Build();

            string storageUrl = config["StorageAccountSettings:Url"]!;
            string tenantId = config["StorageAccountSettings:TenantId"]!;

            var credentialOptions = new DefaultAzureCredentialOptions
            {
                SharedTokenCacheTenantId = tenantId,
                VisualStudioCodeTenantId = tenantId,
                VisualStudioTenantId = tenantId,
                ExcludeEnvironmentCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeInteractiveBrowserCredential = true
            };

            var credential = new DefaultAzureCredential(credentialOptions);

            _blobServiceClient = new BlobServiceClient(new Uri(storageUrl), credential);
            _queueServiceClient = new QueueServiceClient(
                new Uri($"https://{StorageAccountName}.queue.core.windows.net"),
                credential,
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            foreach (string containerName in _containerNamesToDelete)
            {
                try
                {
                    var container = _blobServiceClient.GetBlobContainerClient(containerName);
                    await container.DeleteIfExistsAsync();
                }
                catch
                {
                    // Best-effort cleanup – do not fail the test
                }
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Creates an attachment container and uploads a single text blob.</summary>
        private async Task<string> CreateAttachmentContainerWithBlobsAsync(
            string noteId,
            string blobName = "test-attachment.txt",
            string content = "Hello from E2E test attachment")
        {
            string noteIdLower = noteId.ToLower();
            var container = _blobServiceClient.GetBlobContainerClient(noteIdLower);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            _containerNamesToDelete.Add(noteIdLower);

            var blobClient = container.GetBlobClient(blobName);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var ms = new MemoryStream(bytes);
            await blobClient.UploadAsync(ms, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" }
            });

            return noteIdLower;
        }

        /// <summary>Sends a ZipRequest message to the queue.</summary>
        private async Task EnqueueZipRequestAsync(string noteId, string zipFileId)
        {
            var message = new { noteId, zipFileId };
            string json = JsonSerializer.Serialize(message);
            var queueClient = _queueServiceClient.GetQueueClient(QueueName);
            await queueClient.SendMessageAsync(json);
        }

        /// <summary>
        /// Polls until the expected zip blob exists in the <c>{noteId}-zip</c> container
        /// or the timeout expires.
        /// </summary>
        private async Task WaitForZipBlobAsync(string noteId, string zipFileId, int maxWaitSeconds = 90)
        {
            string zipContainerName = $"{noteId.ToLower()}-zip";
            var containerClient = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            var blobClient = containerClient.GetBlobClient(zipFileId);

            DateTime deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (await containerClient.ExistsAsync() && await blobClient.ExistsAsync())
                    return;

                await Task.Delay(3000);
            }

            throw new TimeoutException(
                $"Zip blob '{zipFileId}' did not appear in container '{zipContainerName}' within {maxWaitSeconds}s.");
        }

        // ─── Tests ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Function_CreatesZipBlob_WhenAttachmentContainerHasBlobs()
        {
            // Arrange
            string noteId = Guid.NewGuid().ToString();
            string zipFileId = $"{Guid.NewGuid()}.zip";
            string zipContainerName = $"{noteId.ToLower()}-zip";
            _containerNamesToDelete.Add(zipContainerName);

            await CreateAttachmentContainerWithBlobsAsync(noteId);

            // Act – enqueue message and wait for function to produce zip
            await EnqueueZipRequestAsync(noteId, zipFileId);
            await WaitForZipBlobAsync(noteId, zipFileId, maxWaitSeconds: 90);

            // Assert – zip blob exists
            var zipContainer = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            bool containerExists = await zipContainer.ExistsAsync();
            containerExists.Should().BeTrue();

            var zipBlobClient = zipContainer.GetBlobClient(zipFileId);
            bool blobExists = await zipBlobClient.ExistsAsync();
            blobExists.Should().BeTrue();
        }

        [Fact]
        public async Task Function_CreatesZipContainingAllAttachmentBlobs()
        {
            // Arrange – upload multiple attachments
            string noteId = Guid.NewGuid().ToString();
            string zipFileId = $"{Guid.NewGuid()}.zip";
            string zipContainerName = $"{noteId.ToLower()}-zip";
            _containerNamesToDelete.Add(zipContainerName);

            string noteIdLower = noteId.ToLower();
            var attachmentContainer = _blobServiceClient.GetBlobContainerClient(noteIdLower);
            await attachmentContainer.CreateIfNotExistsAsync(PublicAccessType.None);
            _containerNamesToDelete.Add(noteIdLower);

            // Upload 3 blobs
            for (int i = 1; i <= 3; i++)
            {
                var blobClient = attachmentContainer.GetBlobClient($"file{i}.txt");
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"Content of file {i}");
                using var ms = new MemoryStream(bytes);
                await blobClient.UploadAsync(ms, overwrite: true);
            }

            // Act
            await EnqueueZipRequestAsync(noteId, zipFileId);
            await WaitForZipBlobAsync(noteId, zipFileId, maxWaitSeconds: 90);

            // Assert – zip blob is non-empty and is a valid zip archive
            var zipContainer = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            var zipBlobClient = zipContainer.GetBlobClient(zipFileId);
            var download = await zipBlobClient.DownloadContentAsync();
            byte[] zipBytes = download.Value.Content.ToArray();
            zipBytes.Length.Should().BeGreaterThan(0);

            // Verify it's a valid zip (PK magic bytes: 0x50 0x4B)
            zipBytes[0].Should().Be(0x50);
            zipBytes[1].Should().Be(0x4B);

            // Verify all 3 attachments are inside the zip
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
            archive.Entries.Select(e => e.Name).Should().Contain(new[] { "file1.txt", "file2.txt", "file3.txt" });
        }

        [Fact]
        public async Task Function_SetsContentTypeToApplicationZip()
        {
            // Arrange
            string noteId = Guid.NewGuid().ToString();
            string zipFileId = $"{Guid.NewGuid()}.zip";
            string zipContainerName = $"{noteId.ToLower()}-zip";
            _containerNamesToDelete.Add(zipContainerName);

            await CreateAttachmentContainerWithBlobsAsync(noteId);

            // Act
            await EnqueueZipRequestAsync(noteId, zipFileId);
            await WaitForZipBlobAsync(noteId, zipFileId, maxWaitSeconds: 90);

            // Assert – blob ContentType is application/zip
            var zipContainer = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            var zipBlobClient = zipContainer.GetBlobClient(zipFileId);
            var properties = await zipBlobClient.GetPropertiesAsync();
            properties.Value.ContentType.Should().Be("application/zip");
        }

        [Fact]
        public async Task Function_DoesNotCreateZip_WhenAttachmentContainerIsEmpty()
        {
            // Arrange – create an empty attachment container
            string noteId = Guid.NewGuid().ToString();
            string zipFileId = $"{Guid.NewGuid()}.zip";
            string zipContainerName = $"{noteId.ToLower()}-zip";
            string noteIdLower = noteId.ToLower();
            _containerNamesToDelete.Add(noteIdLower);

            var attachmentContainer = _blobServiceClient.GetBlobContainerClient(noteIdLower);
            await attachmentContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            // Act – enqueue; function should skip zip creation
            await EnqueueZipRequestAsync(noteId, zipFileId);

            // Wait a reasonable time and verify no zip container/blob was created
            await Task.Delay(30_000);

            var zipContainer = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            bool zipContainerExists = await zipContainer.ExistsAsync();

            // Container should not have been created (or blob should not exist)
            if (zipContainerExists)
            {
                var blobClient = zipContainer.GetBlobClient(zipFileId);
                bool blobExists = await blobClient.ExistsAsync();
                blobExists.Should().BeFalse("zip blob should not be created if attachment container is empty");
            }
            else
            {
                zipContainerExists.Should().BeFalse();
            }
        }

        [Fact]
        public async Task Function_DoesNotCreateZip_WhenAttachmentContainerDoesNotExist()
        {
            // Arrange – do NOT create an attachment container
            string noteId = Guid.NewGuid().ToString();
            string zipFileId = $"{Guid.NewGuid()}.zip";
            string zipContainerName = $"{noteId.ToLower()}-zip";

            // Act – enqueue; function should log warning and return without creating zip
            await EnqueueZipRequestAsync(noteId, zipFileId);

            // Wait and verify no zip was created
            await Task.Delay(30_000);

            var zipContainer = _blobServiceClient.GetBlobContainerClient(zipContainerName);
            bool zipContainerExists = await zipContainer.ExistsAsync();
            zipContainerExists.Should().BeFalse(
                "zip container should not be created when attachment container does not exist");
        }
    }
}
