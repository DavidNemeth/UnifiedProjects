using System.Net.Http.Json;
using USheets.Dtos;

namespace USheets.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;

    public UserService(HttpClient httpClient, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            // The request URI is relative to the HttpClient's BaseAddress
            var user = await _httpClient.GetFromJsonAsync<UserDto>("/api/UserInfo/me");
            return user;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching current user from UPortal API.");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing user data from UPortal API.");
            return null;
        }
        catch (System.Exception ex) // Catch any other unexpected exceptions
        {
            _logger.LogError(ex, "An unexpected error occurred while fetching current user.");
            return null;
        }
    }
}
