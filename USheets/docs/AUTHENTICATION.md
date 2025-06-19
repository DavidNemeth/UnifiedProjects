# USheets Authentication System

USheets does not implement its own user authentication logic. Instead, it relies entirely on **UPortal** as its central authentication provider. This approach allows for a Single Sign-On (SSO) experience: once a user is logged into UPortal, they are automatically authenticated in USheets.

## Authentication Flow

1.  **Prerequisite: Authenticated via UPortal:**
    *   The user must first authenticate with UPortal (e.g., by visiting the UPortal application and logging in via Azure AD).
    *   Upon successful login, UPortal sets an authentication cookie on a shared parent domain (e.g., `.my-app.local`). This cookie is accessible to UPortal and any other application hosted on a subdomain of `my-app.local`, including USheets (e.g., `usheets.my-app.local`).

2.  **Accessing USheets:**
    *   When the user navigates to the USheets application, their browser automatically sends the shared authentication cookie with the request to the USheets server.

3.  **User Information Retrieval (Client-Side Blazor):**
    *   USheets is a Blazor WebAssembly (WASM) application. The authentication state is managed client-side.
    *   **`ApiAuthenticationStateProvider`:** This custom `AuthenticationStateProvider` (located in `USheets/Services/ApiAuthenticationStateProvider.cs`) is responsible for determining the current user's authentication state.
    *   **`IUserService` (`UserService.cs`):** The `ApiAuthenticationStateProvider` uses an implementation of `IUserService` (specifically `UserService.cs`) to fetch the current user's details.
    *   **Call to UPortal API:** The `UserService.GetCurrentUserAsync()` method makes an HTTP GET request to UPortal's `/api/UserInfo/me` endpoint. This request inherently includes the shared authentication cookie, which UPortal uses to identify and authenticate the user.
    *   **Receiving User Data:** UPortal responds with a `UserDto` (defined in USheets, but populated from UPortal's `AppUserDto`) containing the user's information, including their ID, name, Azure AD Object ID, and importantly, their assigned role names.

4.  **Establishing Claims Principal:**
    *   If the `/api/UserInfo/me` call is successful and returns valid user data (and the user is marked as active), the `ApiAuthenticationStateProvider` creates a `ClaimsPrincipal` for the user.
    *   This `ClaimsPrincipal` includes:
        *   Basic user identity claims (Name, NameIdentifier, AzureAdObjectId).
        *   Role claims (`ClaimTypes.Role`) for each role name returned by UPortal.
    *   If the API call fails or returns no user, an anonymous `ClaimsPrincipal` is created, meaning the user is treated as unauthenticated by USheets.

5.  **Authorization in USheets:**
    *   Once the `ClaimsPrincipal` is established, USheets can use standard Blazor authorization mechanisms (e.g., `<AuthorizeView Roles="Manager">`, `[Authorize(Roles = "Manager")]`) to control access to pages, components, and features based on the user's roles.
    *   These roles originate from UPortal's user management system.

## Key Components in USheets

*   **`BrowserRequestCredentialsMessageHandler` (`USheets/Services/BrowserRequestCredentialsMessageHandler.cs`):**
    *   This message handler is configured for the `HttpClient` used by `UserService`. Its purpose is to ensure that browser credentials (like cookies) are included in requests made by the `HttpClient` to the UPortal API. This is crucial for the `/api/UserInfo/me` call to be authenticated.
    *   It sets `SetBrowserRequestCredentials(BrowserRequestCredentials.Include)` for the request.

*   **`Program.cs` (Client-Side):**
    *   Configures the dependency injection for `IUserService`, `ApiAuthenticationStateProvider`, and the `HttpClient` used by `UserService`.
    *   The `HttpClient`'s `BaseAddress` is typically configured to point to the UPortal application's base URL so that the relative path `/api/UserInfo/me` resolves correctly.

## Summary

USheets' authentication is essentially a delegation to UPortal. It trusts UPortal to perform the actual user login and relies on a shared cookie and an API call to UPortal to obtain the necessary user information and roles to establish an authenticated session within the Blazor application.
```
