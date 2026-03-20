using HW4NoteKeeper.BlazorUI.Services;
using HW4NoteKeeper.RequestAndResultObjects;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Text.Json;

namespace HW4NoteKeeper.BlazorUI.Components.Pages
{
    /// <summary>
    /// Code-behind for the Home page component that provides a UI for testing the NoteKeeper API.
    /// Includes functionality for CRUD operations on notes.
    /// </summary>
    public partial class Home
    {
        [Inject]
        private NoteKeeperApiService ApiService { get; set; } = default!;

        // Input fields
        private string getNoteId = "";
        private string createNoteJson = @"{
  ""summary"": ""hello world"",
  ""details"": ""GitHub Copilot Chat is a companion extension to GitHub Copilot that allows you to chat with Copilot, an AI-powered assistant that helps you write better code. With GitHub Copilot Chat, you can access two key features: Chat View: Seek Copilot's assistance for any task or question within the Chat view. Inline Refinement: Apply Copilot's suggestions directly to your code, seamlessly maintaining your workflow.""
}";
        private string updateNoteJson = @"{
  ""summary"": ""hello world (updated)"",
  ""details"": ""GitHub Copilot Chat is a companion extension to GitHub Copilot that allows you to chat with Copilot, an AI-powered assistant that helps you write better code. With GitHub Copilot Chat, you can access two key features: Chat View: Seek Copilot's assistance for any task or question within the Chat view. Inline Refinement: Apply Copilot's suggestions directly to your code, seamlessly maintaining your workflow.""
}";
        private string updateNoteId = "";
        private string deleteNoteId = "";

        // Response fields
        private string getAllStatusCode = "";
        private string getAllResponse = "";
        private string getByIdStatusCode = "";
        private string getByIdResponse = "";
        private string createStatusCode = "";
        private string createResponse = "";
        private string updateStatusCode = "";
        private string updateResponse = "";
        private string deleteStatusCode = "";
        private string deleteResponse = "";

        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Retrieves all notes from the API and displays them.
        /// </summary>
        private async Task GetAllNotes()
        {
            var (statusCode, body) = await ApiService.GetAllNotesAsync();
            getAllStatusCode = $"{(int)statusCode} {statusCode}";
            getAllResponse = FormatJson(body);
        }

        /// <summary>
        /// Retrieves a specific note by its ID and displays the result.
        /// </summary>
        private async Task GetNoteById()
        {
            if (string.IsNullOrWhiteSpace(getNoteId))
            {
                getByIdStatusCode = "400 Bad Request";
                getByIdResponse = "Note ID is required.";
                return;
            }

            var (statusCode, body) = await ApiService.GetNoteByIdAsync(getNoteId);
            getByIdStatusCode = $"{(int)statusCode} {statusCode}";
            getByIdResponse = FormatJson(body);
        }

        /// <summary>
        /// Creates a new note from the JSON input and displays the result.
        /// Validates the JSON format before sending to the API.
        /// </summary>
        private async Task CreateNote()
        {
            try
            {
                var request = JsonSerializer.Deserialize<CreateNoteRequest>(createNoteJson, jsonOptions);
                if (request == null)
                {
                    createStatusCode = "400 Bad Request";
                    createResponse = "Invalid JSON format.";
                    return;
                }

                var (statusCode, body) = await ApiService.CreateNoteAsync(request);
                createStatusCode = $"{(int)statusCode} {statusCode}";
                createResponse = FormatJson(body);
            }
            catch (JsonException)
            {
                createStatusCode = "400 Bad Request";
                createResponse = "Invalid JSON format.";
            }
        }

        /// <summary>
        /// Updates an existing note with new summary and/or details.
        /// Validates the JSON format and requires a note ID before sending to the API.
        /// </summary>
        private async Task UpdateNote()
        {
            if (string.IsNullOrWhiteSpace(updateNoteId))
            {
                updateStatusCode = "400 Bad Request";
                updateResponse = "Note ID is required.";
                return;
            }

            try
            {
                var request = JsonSerializer.Deserialize<UpdateNoteRequest>(updateNoteJson, jsonOptions);
                if (request == null)
                {
                    updateStatusCode = "400 Bad Request";
                    updateResponse = "Invalid JSON format.";
                    return;
                }

                var (statusCode, body) = await ApiService.UpdateNoteAsync(updateNoteId, request);
                updateStatusCode = $"{(int)statusCode} {statusCode}";
                updateResponse = string.IsNullOrWhiteSpace(body) ? "Note updated successfully." : FormatJson(body);
            }
            catch (JsonException)
            {
                updateStatusCode = "400 Bad Request";
                updateResponse = "Invalid JSON format.";
            }
        }

        /// <summary>
        /// Deletes a note by its ID.
        /// Requires a note ID before sending the delete request to the API.
        /// </summary>
        private async Task DeleteNote()
        {
            if (string.IsNullOrWhiteSpace(deleteNoteId))
            {
                deleteStatusCode = "400 Bad Request";
                deleteResponse = "Note ID is required.";
                return;
            }

            var (statusCode, body) = await ApiService.DeleteNoteAsync(deleteNoteId);
            deleteStatusCode = $"{(int)statusCode} {statusCode}";
            deleteResponse = string.IsNullOrWhiteSpace(body) ? "Note deleted successfully." : FormatJson(body);
        }

        /// <summary>
        /// Formats a JSON string with proper indentation for display.
        /// Returns the original string if JSON parsing fails.
        /// </summary>
        /// <param name="json">The JSON string to format.</param>
        /// <returns>A formatted JSON string or the original string if formatting fails.</returns>
        private string FormatJson(string json)
        {
            try
            {
                var obj = JsonSerializer.Deserialize<object>(json);
                return JsonSerializer.Serialize(obj, jsonOptions);
            }
            catch
            {
                return json;
            }
        }
    }
}

