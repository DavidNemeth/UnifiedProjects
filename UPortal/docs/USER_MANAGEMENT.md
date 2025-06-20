# UPortal User Management System

UPortal employs a Role-Based Access Control (RBAC) system to manage user access to its features and potentially to features in integrated applications. This system is built on top of the users authenticated via the process described in `AUTHENTICATION.md`.

## Core Concepts

The user management system revolves around three main entities: Users, Roles, and Permissions.

1.  **Users (`AppUser`):**
    *   Represents an individual who can log in to the system.
    *   User accounts are typically provisioned or synchronized based on their Azure AD identity when they first log in via UPortal (see `IAppUserService.CreateOrUpdateUserFromAzureAdAsync`).
    *   Each user in the UPortal database has a unique ID and stores details like their name, email, and a reference to their Azure AD Object ID.
    *   Users can be assigned one or more roles.
    *   Managed primarily by `AppUserService` (`IAppUserService`).

2.  **Roles (`Role`):**
    *   Represent a collection of permissions. Roles define a set of actions a user can perform or access they are granted.
    *   Examples of roles might include "Administrator", "Manager", "Editor", or application-specific roles.
    *   Roles are created and managed within UPortal by administrators.
    *   Permissions are assigned to roles. A user inherits permissions from all roles they are assigned.
    *   Managed primarily by `RoleService` (`IRoleService`).

3.  **Permissions (`Permission`):**
    *   Represent specific actions or access rights within the application.
    *   Permissions are granular and define what a user can do, e.g., "ViewUsers", "EditSettings", "CreateDocuments".
    *   Permissions are typically predefined within the application code or database.
    *   Permissions are assigned to roles, not directly to users.
    *   Managed primarily by `PermissionService` (`IPermissionService`).

## How It Works

1.  **User Authentication and Identification:**
    *   A user logs in via Azure AD, and UPortal identifies or creates a local `AppUser` record.

2.  **Role Assignment:**
    *   Administrators can assign roles to users through UPortal's user management interface (which uses `AppUserService.AssignRoleToUserAsync`, `AppUserService.RemoveRoleFromUserAsync`, or `AppUserService.UpdateUserRolesAsync`).

3.  **Permission Assignment to Roles:**
    *   Administrators can assign specific permissions to roles (which uses `RoleService.AssignPermissionToRoleAsync` or `RoleService.RemovePermissionFromRoleAsync`).

4.  **Access Control Checks:**
    *   When a user attempts to perform an action or access a resource, the application checks if the user has the required permission.
    *   This check is done by:
        *   Identifying the user's roles.
        *   Determining all permissions granted by those roles.
        *   Verifying if the required permission is among the user's granted permissions.
    *   UPortal uses a custom `PermissionPolicyProvider` and `PermissionHandler` (found in `UPortal/Security/`) to enable attribute-based authorization checks in the application code (e.g., `[Authorize(Policy = "CanViewUsers")]`).
    *   The `IAppUserService.UserHasPermissionAsync(userId, permissionName)` method can also be used directly to check if a user has a specific permission.

## Service Layer

The core logic for managing these entities is encapsulated in the following services:

*   **`IAppUserService` (`AppUserService.cs`):** Handles operations related to users, such as retrieving user details, creating/updating users from Azure AD claims, assigning/removing roles from users, and checking user permissions.
*   **`IRoleService` (`RoleService.cs`):** Manages roles, including creating, updating, deleting roles, and assigning/removing permissions from roles.
*   **`IPermissionService` (`PermissionService.cs`):** Manages permissions, primarily retrieving available permissions. Permissions are often seeded or predefined rather than dynamically created through an API by users.

## User Information Propagation

When an authenticated application (like USheets) fetches user information from UPortal's `/api/UserInfo/me` endpoint, the returned `AppUserDto` includes the names of the roles assigned to the user. This allows the consuming application to understand the user's roles and potentially enforce access control based on them, or use them to determine which UI elements to display. The DTO also includes a flat list of permission names for the user.
```
