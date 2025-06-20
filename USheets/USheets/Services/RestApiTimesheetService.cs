using System.Net; 
using System.Net.Http.Json;
using USheets.Dtos;

namespace USheets.Services
{
    public class RestApiTimesheetService : ITimesheetService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RestApiTimesheetService> _logger;

        // Inject the logger
        public RestApiTimesheetService(HttpClient httpClient, ILogger<RestApiTimesheetService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<TimesheetDto?> GetTimesheetAsync(DateTime weekStartDate)
        {
            var requestUrl = $"/api/timesheets?weekStartDate={weekStartDate:yyyy-MM-dd}";
            _logger.LogInformation("Requesting timesheet from {Url}", requestUrl);

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);

                // If the API returns 204 No Content, it means no timesheet exists for that week.
                // This is an expected, successful scenario, so we return null.
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    _logger.LogInformation("No timesheet found for week starting {WeekStart} (204 No Content).", weekStartDate.ToShortDateString());
                    return null;
                }

                // If we get any other non-success code, throw an exception.
                response.EnsureSuccessStatusCode();

                // If we get here, it was a 200 OK with a valid JSON body.
                return await response.Content.ReadFromJsonAsync<TimesheetDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API request failed when getting timesheet. Status: {StatusCode}", ex.StatusCode);
                throw; // Re-throw so the UI component knows something went wrong.
            }
        }

        public async Task<TimesheetDto> SaveTimesheetAsync(TimesheetCreateUpdateDto dto)
        {
            _logger.LogInformation("Saving timesheet for week starting {WeekStart}", dto.WeekStartDate.ToShortDateString());
            try
            {
                var response = await _httpClient.PutAsJsonAsync("/api/timesheets", dto);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TimesheetDto>();
                return result ?? throw new InvalidOperationException("API did not return a valid timesheet after save.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API request failed when saving timesheet. Status: {StatusCode}", ex.StatusCode);
                throw;
            }
        }

        public async Task<List<TimesheetDto>> GetPendingApprovalTimesheetsAsync()
        {
            _logger.LogInformation("Fetching pending approval timesheets.");
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<TimesheetDto>>("/api/timesheets/pending-approval");
                return result ?? new List<TimesheetDto>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API request failed when fetching pending approvals. Status: {StatusCode}", ex.StatusCode);
                throw;
            }
        }

        public async Task<TimesheetDto> ApproveTimesheetAsync(int timesheetId)
        {
            _logger.LogInformation("Approving timesheet with ID {TimesheetId}", timesheetId);
            try
            {
                var response = await _httpClient.PostAsync($"/api/timesheets/{timesheetId}/approve", null);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TimesheetDto>();
                return result ?? throw new InvalidOperationException("API did not return a valid timesheet after approval.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API request failed when approving timesheet {TimesheetId}. Status: {StatusCode}", timesheetId, ex.StatusCode);
                throw;
            }
        }

        public async Task<TimesheetDto> RejectTimesheetAsync(int timesheetId, string reason)
        {
            _logger.LogInformation("Rejecting timesheet with ID {TimesheetId}", timesheetId);
            try
            {
                var payload = new RejectionReasonModel { Reason = reason };
                var response = await _httpClient.PostAsJsonAsync($"/api/timesheets/{timesheetId}/reject", payload);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TimesheetDto>();
                return result ?? throw new InvalidOperationException("API did not return a valid timesheet after rejection.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "API request failed when rejecting timesheet {TimesheetId}. Status: {StatusCode}", timesheetId, ex.StatusCode);
                throw;
            }
        }
    }
}