using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using USheets.Models;

namespace USheets.Services
{
    /// <summary>
    /// A service for interacting with the timesheet REST API.
    /// This version is modified to throw exceptions on HTTP or serialization failures,
    //  allowing the caller (UI) to handle errors gracefully.
    /// </summary>
    public class RestApiTimesheetService : ITimesheetService
    {
        private readonly HttpClient _httpClient;

        public RestApiTimesheetService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private async Task<T?> ExecuteRequestAsync<T>(Func<Task<HttpResponseMessage>> requestFunc, string errorContextMessage)
        {
            try
            {
                var response = await requestFunc();

                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.Content == null)
                    {
                        return default; // Handles NoContent for GET, or could be void for POST/PUT if not T
                    }
                    // Ensure content is not null before attempting to deserialize, primarily for GET requests
                    // For POST/PUT that might return NoContent but still have IsSuccessStatusCode, this check is also useful.
                    if (response.Content != null && response.Content.Headers.ContentLength > 0)
                    {
                        return await response.Content.ReadFromJsonAsync<T>();
                    }
                    return default; // Return default if content is null or empty but success
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error {errorContextMessage}: {response.StatusCode}. Details: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API request error in {errorContextMessage}: {ex.Message}");
                throw; // Re-throw the original HttpRequestException
            }
            catch (Exception ex) // Catches other errors like JSON parsing
            {
                Console.WriteLine($"An unexpected error occurred in {errorContextMessage}: {ex.Message}");
                throw new TimesheetServiceException($"An unexpected error occurred while {errorContextMessage.ToLower()}.", ex);
            }
        }

        private async Task ExecuteRequestAsync(Func<Task<HttpResponseMessage>> requestFunc, string errorContextMessage)
        {
            try
            {
                var response = await requestFunc();

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error {errorContextMessage}: {response.StatusCode}. Details: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API request error in {errorContextMessage}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred in {errorContextMessage}: {ex.Message}");
                throw new TimesheetServiceException($"An unexpected error occurred while {errorContextMessage.ToLower()}.", ex);
            }
        }

        public async Task<List<TimesheetEntry>?> GetTimesheetEntriesAsync(DateTime weekStartDate)
        {
            var requestUrl = $"/api/Timesheet?weekStartDate={weekStartDate:yyyy-MM-dd}";
            return await ExecuteRequestAsync<List<TimesheetEntry>?>(
                () => _httpClient.GetAsync(requestUrl),
                "fetching timesheet entries"
            );
        }

        public async Task SaveTimesheetEntriesAsync(DateTime weekStartDate, List<TimesheetEntry> entries)
        {
            var requestUrl = $"/api/Timesheet/Week?weekStartDate={weekStartDate:yyyy-MM-dd}";
            await ExecuteRequestAsync(
                () => _httpClient.PostAsJsonAsync(requestUrl, entries),
                "saving timesheet entries"
            );
        }

        public async Task<List<TimesheetEntry>?> CopyTimesheetEntriesFromPreviousWeekAsync(DateTime currentWeekStartDate, DateTime previousWeekStartDate)
        {
            var requestUrl = $"/api/Timesheet/Copy?currentWeekStartDate={currentWeekStartDate:yyyy-MM-dd}&previousWeekStartDate={previousWeekStartDate:yyyy-MM-dd}";
            return await ExecuteRequestAsync<List<TimesheetEntry>?>(
                () => _httpClient.PostAsync(requestUrl, null),
                "copying timesheet entries"
            );
        }
    }

    public class TimesheetServiceException : Exception
    {
        public TimesheetServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}