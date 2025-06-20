using System.Net.Http.Json;
using USheets.Dtos;

namespace USheets.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UserService> _logger;

        public UserService(HttpClient httpClient, ILogger<UserService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AppUserDto?> GetCurrentUserAsync()
        {
            // ... (existing implementation)
            try
            {
                var user = await _httpClient.GetFromJsonAsync<AppUserDto>("/api/UserInfo/me");
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current user info.");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, AppUserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds)
        {
            var idList = userIds?.ToList() ?? new List<int>();
            if (!idList.Any())
            {
                return new Dictionary<int, AppUserDto>();
            }

            try
            {
                // Use PostAsJsonAsync to send the list of IDs in the request body
                var response = await _httpClient.PostAsJsonAsync("/api/UserInfo/batch", idList);

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<AppUserDto>>();
                    if (users != null)
                    {
                        // Convert the list to a dictionary for easy lookups
                        return users.ToDictionary(u => u.Id, u => u);
                    }
                }
                else
                {
                    _logger.LogError("API call to fetch users by ID failed with status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while fetching users by ID.");
            }

            // Return an empty dictionary on failure
            return new Dictionary<int, AppUserDto>();
        }
    }
}