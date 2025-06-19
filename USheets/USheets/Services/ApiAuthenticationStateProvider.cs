using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using USheets.Services;

public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IUserService _userService;
    private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

    public ApiAuthenticationStateProvider(IUserService userService)
    {
        _userService = userService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // This is where we call our API to get the user
            var user = await _userService.GetCurrentUserAsync();
            if (user == null || !user.IsActive)
            {
                // If no user is returned, they are not authenticated.
                return new AuthenticationState(_anonymous);
            }

            // Create the claims identity from the user's information
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("AzureAdObjectId", user.AzureAdObjectId) 
                // Add any other claims you might need from the user object
            }, "ApiAuth");

            // Add roles as separate claims
            if (user.RoleNames != null)
            {
                foreach (var roleName in user.RoleNames)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }

            var claimsPrincipal = new ClaimsPrincipal(identity);

            return new AuthenticationState(claimsPrincipal);
        }
        catch
        {
            // If an error occurs, treat the user as unauthenticated
            return new AuthenticationState(_anonymous);
        }
    }

    /// <summary>
    /// This method can be called to trigger a re-evaluation of the authentication state.
    /// For example, after a user logs in or out.
    /// </summary>
    public void NotifyUserAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}