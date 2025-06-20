# Local Development Setup

This document describes how to set up the U-Suite (`UPortal` and `USheets`) solution on a local development machine.

## Overview

The solution consists of three primary projects that run concurrently:
1.  **`UPortal.Api`**: The main portal, identity provider, and user management API. It handles user login via Azure AD and issues the shared authentication cookie.
2.  **`USheets.Api`**: The microservice API responsible for all timesheet-related data and logic. It relies on the cookie from `UPortal.Api` for authentication.
3.  **`USheets`**: The Blazor WebAssembly frontend application that users interact with.

## Prerequisites

- .NET 8 SDK (or the version used by the project)
- Visual Studio 2022 (with ASP.NET and web development workload) or VS Code
- SQL Server (LocalDB, which comes with Visual Studio, is sufficient)
- Git

## 1. Hosts File Configuration

Because the applications share an authentication cookie across different hostnames, you must map these hostnames to your local machine.

1.  Open **Notepad** (or another text editor) **as an Administrator**.
2.  Open the `hosts` file located at: `C:\Windows\System32\drivers\etc\hosts`
3.  Add the following lines to the end of the file and save it:

    ```
    127.0.0.1    dev.uportal.local
    127.0.0.1    dev.usheet.local
    ```

## 2. HTTPS Certificates

This setup requires trusted local development certificates for the custom hostnames. If you have not already created them for this project, you will need to generate self-signed certificates for `dev.uportal.local` and `dev.usheet.local` and configure them in each project's `appsettings.Development.json` `Kestrel` section.

## 3. Configuration (`appsettings.Development.json`)

Ensure the following configurations are set correctly.

### Shared Key Store
For the two APIs to read the same authentication cookie, they must share a data protection key store.

1.  Create a folder on your machine that is outside of any git repository. For example: `C:\U-Suite-Keys`.
2.  In **both** `UPortal.Api/appsettings.Development.json` and `USheets.Api/appsettings.Development.json`, make sure the `DataProtection:KeyPath` points to this same folder.

    ```json
    "DataProtection": {
      "KeyPath": "C:\\U-Suite-Keys"
    }
    ```

### Database Connection String
Verify that the `ConnectionStrings:DefaultConnection` in `USheets.Api/appsettings.json` points to your local database instance. The database will be created automatically by the migration step.

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=USheetsDB;Trusted_Connection=True;"
}
```

## 4. Running the Solution

### Database Migrations
Before running the applications for the first time, you must apply the Entity Framework migrations to create the databases.

1.  Open the **Package Manager Console** in Visual Studio.
2.  Set the "Default project" to **`UPortal.Api`** and run: `Update-Database`
3.  Set the "Default project" to **`USheets.Api`** and run: `Update-Database`

### Running the Projects
The easiest way to run the solution is to configure Visual Studio to launch all three startup projects at once.

1.  Right-click the Solution in the Solution Explorer and select **Configure Startup Projects...**.
2.  Choose **Multiple startup projects**.
3.  Set the "Action" for the following projects to **Start**:
    * `UPortal.Api`
    * `USheets.Api`
    * `USheets` (the Blazor WASM project)
4.  Click **Apply** and **OK**.

### First-Time Login
1.  Press **F5** or the Start button in Visual Studio to launch all three projects.
2.  Your browser should open to the `USheets` frontend application URL.
3.  You will be redirected to the Microsoft login page to authenticate with Azure AD.
4.  After successfully logging in, you will be redirected back to the application. The `UPortal.Api` will have created the shared `.Auth.UPortal` cookie containing your `InternalUserId` claim.
5.  All subsequent calls from the frontend to both `UPortal.Api` and `USheets.Api` will now be authenticated correctly.