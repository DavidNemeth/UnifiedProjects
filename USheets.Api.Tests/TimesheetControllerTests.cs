using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using USheets.Api; // Assuming USheets.Api.Startup or Program
using USheets.Api.Data;
using USheets.Api.Models;
using Xunit;

// TODO: Create a .csproj file for this test project and add necessary package references:
// - Microsoft.NET.Sdk
// - xunit
// - xunit.runner.visualstudio
// - Microsoft.AspNetCore.Mvc.Testing
// - Microsoft.EntityFrameworkCore.InMemory
// - Moq (or other mocking framework if needed for more complex scenarios)

namespace USheets.Api.Tests
{
    // A custom WebApplicationFactory to configure services for tests, like using an in-memory DB
    public class TestWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the app's ApplicationDbContext registration.
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApiDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add ApplicationDbContext using an in-memory database for testing.
                services.AddDbContext<ApiDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"InMemoryDbForTesting-{Guid.NewGuid()}");
                });

                // TODO: Add services for mocking authentication/authorization if needed.
                // For example, services.AddAuthentication("Test").AddScheme...
            });

            // Use Test environment to potentially load test-specific configurations
            builder.UseEnvironment("Test");
        }
    }

    public class TimesheetControllerTests : IClassFixture<TestWebApplicationFactory<Program>> // Assuming Program.cs is the entry point
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory<Program> _factory;

        public TimesheetControllerTests(TestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, // Optional, but often useful for API tests
            });
        }

        private async Task SeedDatabaseAsync(ApiDbContext context, List<TimesheetEntry> entries)
        {
            if (entries != null && entries.Any())
            {
                await context.TimesheetEntries.AddRangeAsync(entries);
                await context.SaveChangesAsync();
            }
        }

        // Helper to get a HttpClient authenticated as a specific role
        private HttpClient GetAuthenticatedClient(string role = "User", string userId = "test-user-id")
        {
            // This is a simplified way to "mock" authentication for WebApplicationFactory.
            // In a real scenario, you'd replace the authentication handler.
            // For now, we'll assume a test authentication scheme is set up in TestWebApplicationFactory
            // that can interpret a custom header or a pre-configured test user.
            // As a fallback if full auth mocking isn't set up, this won't provide real auth,
            // but allows tests to run. Actual auth testing requires more setup.
            var client = _factory.CreateClient(); // Create a new client instance

            // A common way to pass test user info is via a custom header that a test auth handler picks up.
            // client.DefaultRequestHeaders.Add("X-Test-User-Role", role);
            // client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);

            // For now, to make tests pass without full auth mocking, we can't truly simulate roles.
            // The [Authorize] attributes will likely block if no real auth is configured.
            // We will write tests assuming the role-based access works and needs to be tested.
            // Actual execution of these tests would require TestServer authentication setup.

            // Simulating a JWT token for a manager for endpoints that require it.
            // THIS IS A PLACEHOLDER. Real token generation or test auth handler is needed.
            if (role == "Manager")
            {
                 // This is NOT a real JWT. A test auth handler would need to be configured
                 // in TestWebApplicationFactory to accept such a placeholder or a real test token.
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", GeneratePlaceholderManagerToken(userId));
            }
            return client;
        }

        private string GeneratePlaceholderManagerToken(string userId)
        {
            // In a real test setup, you might use a library to generate a valid-looking test JWT,
            // or have a test authentication handler that accepts simple strings as tokens.
            // This is highly simplified and likely won't work without a custom test auth handler.
            var claims = new[]
            {
                new { type = "sub", value = userId }, // Subject (user ID)
                new { type = "role", value = "Manager" },
                new { type = "name", value = $"Test {userId}" } // Example claim
                // Add other claims as your application expects, e.g., nameidentifier
            };
            // This is a mock "token" - not a real JWT.
            return $"TEST_TOKEN_FOR_{userId.ToUpper()}_ROLE_MANAGER";
        }


        [Fact]
        public async Task GetPendingApproval_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            var client = _factory.CreateClient(); // Unauthenticated client

            // Act
            var response = await client.GetAsync("/api/Timesheet/pending-approval");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPendingApproval_AsManager_NoTimesheets_ReturnsOkAndEmptyList()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync(); // Clean slate
            await context.Database.EnsureCreatedAsync();


            // Act
            var response = await client.GetAsync("/api/Timesheet/pending-approval");

            // Assert
            response.EnsureSuccessStatusCode(); // Status OK
            var timesheets = await response.Content.ReadFromJsonAsync<List<TimesheetEntry>>();
            Assert.NotNull(timesheets);
            Assert.Empty(timesheets);
        }

        [Fact]
        public async Task GetPendingApproval_AsManager_WithSubmittedTimesheets_ReturnsOkAndTimesheets()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            var submittedTimesheets = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Date = DateTime.UtcNow.Date, Status = TimesheetStatus.Submitted, TotalHours = 8, ProjectName = "P1" },
                new TimesheetEntry { Id = 2, Date = DateTime.UtcNow.Date, Status = TimesheetStatus.Draft, TotalHours = 8, ProjectName = "P2"  },
                new TimesheetEntry { Id = 3, Date = DateTime.UtcNow.Date, Status = TimesheetStatus.Submitted, TotalHours = 6, ProjectName = "P3"  }
            };
            await SeedDatabaseAsync(context, submittedTimesheets);

            // Act
            var response = await client.GetAsync("/api/Timesheet/pending-approval");

            // Assert
            response.EnsureSuccessStatusCode();
            var timesheets = await response.Content.ReadFromJsonAsync<List<TimesheetEntry>>();
            Assert.NotNull(timesheets);
            Assert.Equal(2, timesheets.Count(ts => ts.Status == TimesheetStatus.Submitted));
            Assert.Contains(timesheets, ts => ts.Id == 1);
            Assert.Contains(timesheets, ts => ts.Id == 3);
        }

        [Fact]
        public async Task ApproveTimesheet_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsync("/api/Timesheet/1/approve", null);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ApproveTimesheet_AsManager_ValidSubmittedTimesheet_ReturnsOkAndUpdatesStatus()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var timesheetId = 1;
            await SeedDatabaseAsync(context, new List<TimesheetEntry> { new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Submitted, Date = DateTime.UtcNow, TotalHours = 8 } });

            // Act
            var response = await client.PostAsync($"/api/Timesheet/{timesheetId}/approve", null);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedTimesheet = await response.Content.ReadFromJsonAsync<TimesheetEntry>();
            Assert.NotNull(updatedTimesheet);
            Assert.Equal(TimesheetStatus.Approved, updatedTimesheet.Status);

            var dbTimesheet = await context.TimesheetEntries.FindAsync(timesheetId);
            Assert.Equal(TimesheetStatus.Approved, dbTimesheet?.Status);
        }

        [Fact]
        public async Task ApproveTimesheet_AsManager_NonExistentTimesheet_ReturnsNotFound()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync(); // Ensure clean DB
            await context.Database.EnsureCreatedAsync();

            // Act
            var response = await client.PostAsync("/api/Timesheet/999/approve", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ApproveTimesheet_AsManager_TimesheetNotSubmitted_ReturnsBadRequest()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var timesheetId = 1;
            await SeedDatabaseAsync(context, new List<TimesheetEntry> { new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Draft, Date = DateTime.UtcNow } });


            // Act
            var response = await client.PostAsync($"/api/Timesheet/{timesheetId}/approve", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }


        [Fact]
        public async Task RejectTimesheet_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            var client = _factory.CreateClient();
            var content = JsonContent.Create(new { Reason = "Test Rejection" });

            // Act
            var response = await client.PostAsync("/api/Timesheet/1/reject", content);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RejectTimesheet_AsManager_ValidSubmittedTimesheet_ReturnsOkAndUpdatesStatusAndReason()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var timesheetId = 1;
            var rejectionReason = "Entries are incorrect.";
            await SeedDatabaseAsync(context, new List<TimesheetEntry> { new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Submitted, Date = DateTime.UtcNow } });
            var payload = new { Reason = rejectionReason };
            var content = JsonContent.Create(payload);

            // Act
            var response = await client.PostAsync($"/api/Timesheet/{timesheetId}/reject", content);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedTimesheet = await response.Content.ReadFromJsonAsync<TimesheetEntry>();
            Assert.NotNull(updatedTimesheet);
            Assert.Equal(TimesheetStatus.Rejected, updatedTimesheet.Status);
            Assert.Equal(rejectionReason, updatedTimesheet.RejectionReason);

            var dbTimesheet = await context.TimesheetEntries.FindAsync(timesheetId);
            Assert.Equal(TimesheetStatus.Rejected, dbTimesheet?.Status);
            Assert.Equal(rejectionReason, dbTimesheet?.RejectionReason);
        }

        [Fact]
        public async Task RejectTimesheet_AsManager_MissingReason_ReturnsBadRequest()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
             using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var timesheetId = 1;
             await SeedDatabaseAsync(context, new List<TimesheetEntry> { new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Submitted, Date = DateTime.UtcNow } });
            var payload = new { Reason = "" }; // Empty reason
            var content = JsonContent.Create(payload);

            // Act
            var response = await client.PostAsync($"/api/Timesheet/{timesheetId}/reject", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RejectTimesheet_AsManager_NonExistentTimesheet_ReturnsNotFound()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var content = JsonContent.Create(new { Reason = "Test Rejection" });


            // Act
            var response = await client.PostAsync("/api/Timesheet/999/reject", content);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RejectTimesheet_AsManager_TimesheetNotSubmitted_ReturnsBadRequest()
        {
            // Arrange
            var client = GetAuthenticatedClient("Manager");
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            var timesheetId = 1;
            await SeedDatabaseAsync(context, new List<TimesheetEntry> { new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Approved, Date = DateTime.UtcNow } });
            var content = JsonContent.Create(new { Reason = "Test Rejection" });

            // Act
            var response = await client.PostAsync($"/api/Timesheet/{timesheetId}/reject", content);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
