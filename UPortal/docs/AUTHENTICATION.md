# UPortal Authentication System

UPortal serves as the central authentication authority for itself and other integrated applications like USheets. It handles user login, session management, and provides user information to other services.

## Key Components and Flow

1.  **Azure Active Directory (Azure AD) Integration:**
    *   UPortal delegates the primary authentication process to Azure AD. Users sign in with their Azure AD credentials.
    *   Upon successful authentication with Azure AD, UPortal receives the user's identity information (e.g., Azure AD Object ID, name, email).

2.  **Local User Provisioning:**
    *   The `AppUserService` (specifically the `CreateOrUpdateUserFromAzureAdAsync` method) in UPortal is responsible for creating or updating a local user record in the UPortal database based on the information received from Azure AD. This local record allows UPortal to manage application-specific details, roles, and permissions.

3.  **Shared Authentication Cookie:**
    *   After successful authentication and local user provisioning/retrieval, UPortal issues a secure, HTTP-only authentication cookie.
    *   This cookie is scoped to a common parent domain (e.g., `.my-app.local`, as configured in `Program.cs` during authentication setup). This allows the cookie to be sent by the browser to any application running on subdomains of this parent domain (e.g., `uportal.my-app.local`, `usheets.my-app.local`).
    *   **Security Attributes:**
        *   **HTTPOnly:** Prevents access via client-side JavaScript, mitigating XSS risks.
        *   **Secure:** Ensures the cookie is transmitted only over HTTPS.
        *   **SameSite:** Typically configured as `Lax` or `Strict` to protect against CSRF attacks. The exact configuration depends on cross-domain requirements.
        *   **Path:** Usually set to `/` to be available across the entire domain.

4.  **Session Management:**
    *   The authentication cookie represents the user's session with UPortal and, by extension, other applications that trust this cookie.
    *   The cookie has an expiration time, managed by ASP.NET Core Identity.

5.  **Providing User Information to Other Applications:**
    *   Authenticated applications (those that have received the shared cookie) can retrieve information about the currently logged-in user by making a GET request to the `/api/UserInfo/me` endpoint provided by UPortal.
    *   This endpoint is part of the `UserInfoController` in UPortal.
    *   The request to `/api/UserInfo/me` must include the shared authentication cookie. UPortal validates this cookie to authenticate the request.
    *   The endpoint returns a JSON object (`AppUserDto`) containing user details such as their ID, name, email, Azure AD Object ID, assigned roles, and permissions.

## How Other Applications Leverage This System

*   When a user navigates to an integrated application (e.g., USheets) after authenticating with UPortal, their browser automatically sends the shared authentication cookie.
*   The integrated application validates this cookie (often by calling back to UPortal or using shared cryptographic keys if the cookie is self-contained like a JWT, though UPortal uses an opaque cookie that UPortal itself validates).
*   If the application needs more user details, it calls the `/api/UserInfo/me` endpoint on UPortal, sending the cookie along with the request.

This system provides a Single Sign-On (SSO) experience, where users log in once to UPortal and are then automatically authenticated for all other connected applications within the same domain.
