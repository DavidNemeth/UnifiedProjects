using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UPortal.Api; // Assuming UPortal.Api.Startup or Program
using UPortal.Data;
using UPortal.Data.Models; // For Role, Permission, AppUser, UserRole etc.
using UPortal.Dtos; // For AppUserDto
using Xunit;

// TODO: Create a .csproj file for this test project and add necessary package references:
// - Microsoft.NET.Sdk
// - xunit
// - xunit.runner.visualstudio
// - Microsoft.AspNetCore.Mvc.Testing
// - Microsoft.EntityFrameworkCore.InMemory

namespace UPortal.Api.Tests
{
    // A custom WebApplicationFactory to configure services for tests, like using an in-memory DB
    // This would be specific to UPortal.Api
    public class UPortalTestWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"UPortalInMemoryDbForTesting-{Guid.NewGuid()}");
                });

                // TODO: Add services for mocking authentication/authorization if needed.
                // This is crucial for testing UserInfoController.
            });
            builder.UseEnvironment("Test");
        }
    }

    public class UserInfoControllerTests : IClassFixture<UPortalTestWebApplicationFactory<Program>> // Assuming Program.cs
    {
        private readonly HttpClient _client;
        private readonly UPortalTestWebApplicationFactory<Program> _factory;

        public UserInfoControllerTests(UPortalTestWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // Placeholder for generating a test token or configuring test authentication
        private HttpClient GetAuthenticatedClientWithManagerRole(string azureAdObjectId = "test-manager-oid")
        {
            var client = _factory.CreateClient();
            // THIS IS A PLACEHOLDER. Real token generation or test auth handler is needed.
            // The TestWebApplicationFactory would need to be configured with a test authentication handler
            // that can interpret this token or a custom header.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", $"TEST_TOKEN_FOR_{azureAdObjectId}_ROLE_MANAGER");
            // The UserInfoController uses User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            // The test auth handler MUST provide this claim based on the test token.
            return client;
        }

        [Fact]
        public async Task GetCurrentUser_AsManager_ReturnsUserDataWithManagerRole()
        {
            // Arrange
            var managerAzureAdObjectId = "manager-user-oid-123";
            var client = GetAuthenticatedClientWithManagerRole(managerAzureAdObjectId);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync(); // Clean slate
            await context.Database.EnsureCreatedAsync();

            // Seed necessary data: Location, Manager Role, Permission (if Role DTO includes them), User, UserRole
            var location = new Location { Id = 1, Name = "Test Location" };
            await context.Locations.AddAsync(location);

            // Seed the "Manager" role and any permissions it might need if AppUserDto detailed them
            // For this test, we primarily care about the RoleName "Manager"
            var managerRole = new Role { Name = "Manager" };
            await context.Roles.AddAsync(managerRole);
            await context.SaveChangesAsync(); // Save to get managerRole.Id

            var testUser = new AppUser
            {
                Name = "Test Manager User",
                AzureAdObjectId = managerAzureAdObjectId,
                IsActive = true,
                LocationId = location.Id
            };
            await context.AppUsers.AddAsync(testUser);
            await context.SaveChangesAsync(); // Save to get testUser.Id

            // Assign the "Manager" role to the test user
            await context.UserRoles.AddAsync(new UserRole { AppUserId = testUser.Id, RoleId = managerRole.Id });
            await context.SaveChangesAsync();

            // Act
            var response = await client.GetAsync("/api/UserInfo/me");

            // Assert
            response.EnsureSuccessStatusCode(); // Status OK
            var userDto = await response.Content.ReadFromJsonAsync<AppUserDto>();

            Assert.NotNull(userDto);
            Assert.Equal(testUser.Name, userDto.Name);
            Assert.Equal(managerAzureAdObjectId, userDto.AzureAdObjectId);
            Assert.Contains("Manager", userDto.RoleNames);
        }

        [Fact]
        public async Task GetCurrentUser_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            var unauthenticatedClient = _factory.CreateClient();

            // Act
            var response = await unauthenticatedClient.GetAsync("/api/UserInfo/me");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
