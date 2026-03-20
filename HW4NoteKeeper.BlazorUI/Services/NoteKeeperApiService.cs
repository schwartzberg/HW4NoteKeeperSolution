using HW4NoteKeeper.RequestAndResultObjects;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HW4NoteKeeper.BlazorUI.Services
{
    /// <summary>
    /// Service for calling the NoteKeeper REST API.
    /// Provides methods to perform CRUD operations on notes via HTTP.
    /// </summary>
    public class NoteKeeperApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoteKeeperApiService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client configured with the API base address.</param>
        public NoteKeeperApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Retrieves all notes from the API.
        /// </summary>
        /// <returns>
        /// A tuple containing the HTTP status code and the response body as a JSON string.
        /// Returns 200 OK with a list of notes, or 500 Internal Server Error if an exception occurs.
        /// </returns>
        public async Task<(HttpStatusCode StatusCode, string ResponseBody)> GetAllNotesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/NoteKeeper");
                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body);
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific note by its unique identifier.
        /// </summary>
        /// <param name="noteId">The unique identifier of the note to retrieve.</param>
        /// <returns>
        /// A tuple containing the HTTP status code and the response body as a JSON string.
        /// Returns 200 OK with the note details, 400 Bad Request if noteId is invalid,
        /// 404 Not Found if the note doesn't exist, or 500 Internal Server Error if an exception occurs.
        /// </returns>
        public async Task<(HttpStatusCode StatusCode, string ResponseBody)> GetNoteByIdAsync(string noteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/NoteKeeper/{noteId}");
                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body);
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new note with AI-generated tags.
        /// </summary>
        /// <param name="request">The note creation request containing summary and details.</param>
        /// <returns>
        /// A tuple containing the HTTP status code and the response body as a JSON string.
        /// Returns 201 Created with the new note (including generated tags),
        /// 400 Bad Request if validation fails, or 500 Internal Server Error if an exception occurs.
        /// </returns>
        public async Task<(HttpStatusCode StatusCode, string ResponseBody)> CreateNoteAsync(CreateNoteRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/NoteKeeper", request);
                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body);
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing note's summary and/or details.
        /// Tags are regenerated if the details are changed.
        /// </summary>
        /// <param name="noteId">The unique identifier of the note to update.</param>
        /// <param name="request">The update request with new summary and/or details. At least one field must be provided.</param>
        /// <returns>
        /// A tuple containing the HTTP status code and the response body as a JSON string.
        /// Returns 204 No Content on success, 400 Bad Request if validation fails,
        /// 404 Not Found if the note doesn't exist, 405 Method Not Allowed if noteId is empty,
        /// or 500 Internal Server Error if an exception occurs.
        /// </returns>
        public async Task<(HttpStatusCode StatusCode, string ResponseBody)> UpdateNoteAsync(string noteId, UpdateNoteRequest request)
        {
            try
            {
                var response = await _httpClient.PatchAsJsonAsync($"/NoteKeeper/{noteId}", request);
                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body);
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, $"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a note permanently from the system.
        /// </summary>
        /// <param name="noteId">The unique identifier of the note to delete.</param>
        /// <returns>
        /// A tuple containing the HTTP status code and the response body as a JSON string.
        /// Returns 204 No Content on success, 404 Not Found if the note doesn't exist,
        /// 405 Method Not Allowed if noteId is empty, or 500 Internal Server Error if an exception occurs.
        /// </returns>
        public async Task<(HttpStatusCode StatusCode, string ResponseBody)> DeleteNoteAsync(string noteId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/NoteKeeper/{noteId}");
                var body = await response.Content.ReadAsStringAsync();
                return (response.StatusCode, body);
            }
            catch (Exception ex)
            {
                return (HttpStatusCode.InternalServerError, $"Exception: {ex.Message}");
            }
        }
    }
}
