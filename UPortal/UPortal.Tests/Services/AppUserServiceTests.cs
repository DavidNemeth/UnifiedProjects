using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UPortal.Data;
using UPortal.Data.Models;
using UPortal.Dtos;
using UPortal.Services;
using Microsoft.Extensions.Logging;
using System.Threading; // Required for CancellationToken

namespace UPortal.Tests.Services
{
    // Helper extension for Moq ILogger verification
    public static class LoggerVerifyExtensions
    {
        public static void VerifyLogging<T>(this Mock<ILogger<T>> mockLogger, LogLevel expectedLogLevel, string expectedMessageContains, Times? times = null, Exception? expectedException = null)
        {
            times ??= Times.AtLeastOnce();

            mockLogger.Verify(
                x => x.Log(
                    expectedLogLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessageContains)),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                times.Value);
        }
    }

    [TestClass]
    public class AppUserServiceTests
    {
        private DbContextOptions<ApplicationDbContext> _options;
        private Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
        private Mock<ILogger<AppUserService>> _mockLogger;
        private Mock<IFinancialService> _mockFinancialService; // Added
        private AppUserService _userService;

        [TestInitialize]
        public void Initialize()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // Seed a default location for tests that might require it due to FK constraints
            using (var context = new ApplicationDbContext(_options))
            {
                context.Database.EnsureCreated(); // Important for In-Memory
                if (!context.Locations.Any(l => l.Id == 1))
                {
                    context.Locations.Add(new Location { Id = 1, Name = "Default Location" });
                    context.SaveChanges();
                }
                 // Ensure the context is clean for AppUsers before each test
                context.AppUsers.RemoveRange(context.AppUsers);
                context.UserRoles.RemoveRange(context.UserRoles);
                context.Roles.RemoveRange(context.Roles);
                context.Permissions.RemoveRange(context.Permissions);
                context.RolePermissions.RemoveRange(context.RolePermissions);
                context.SaveChanges();
            }

            _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ApplicationDbContext(_options)); // Factory returns new context instance
            _mockDbContextFactory.Setup(f => f.CreateDbContext())
                .Returns(() => new ApplicationDbContext(_options)); // Factory returns new context instance


            _mockLogger = new Mock<ILogger<AppUserService>>();
            _mockFinancialService = new Mock<IFinancialService>(); // Added

            // Default setup for financial service, can be overridden in specific tests
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(It.IsAny<int>()))
                .ReturnsAsync(0m); // Default to 0, tests needing specific values will override

            _userService = new AppUserService(_mockDbContextFactory.Object, _mockLogger.Object, _mockFinancialService.Object); // Added IFinancialService
        }

        private ApplicationDbContext CreateContext() => new ApplicationDbContext(_options); // Returns a new context for seeding/verification

        // Existing tests from the original file (verified they are here)
        #region Existing AppUserService Tests
        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_NullClaimsPrincipal_ThrowsArgNullExceptionAndLogsError()
        {
            ClaimsPrincipal? principal = null;
            var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _userService.CreateOrUpdateUserFromAzureAdAsync(principal!));
            Assert.AreEqual("userPrincipal", exception.ParamName);
            _mockLogger.VerifyLogging(LogLevel.Error, "userPrincipal cannot be null", Times.Once());
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_MissingAzureAdObjectIdClaim_ThrowsArgumentNullExceptionAndLogsError()
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Test User") };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal));
            Assert.AreEqual("azureAdObjectId", exception.ParamName);
            _mockLogger.VerifyLogging(LogLevel.Error, "Azure AD Object ID not found in claims", Times.Once());
        }

        [TestMethod]
        public async Task UpdateAppUserAsync_UserNotFound_ThrowsKeyNotFoundExceptionAndLogsError()
        {
            var nonExistentUserId = 999;
            var userToUpdateDto = new UpdateAppUserDto { IsActive = true, LocationId = 1 };
             // For this test, we ensure the user does not exist by using a fresh context from the factory
            var exception = await Assert.ThrowsExceptionAsync<KeyNotFoundException>(
                () => _userService.UpdateAppUserAsync(nonExistentUserId, userToUpdateDto));
            Assert.IsTrue(exception.Message.Contains($"User with ID {nonExistentUserId} not found."));
            _mockLogger.VerifyLogging(LogLevel.Error, $"User with ID {nonExistentUserId} not found for update.", Times.Once());
        }


        [TestMethod]
        public async Task GetByAzureAdObjectIdAsync_UserExists_ReturnsUserDto()
        {
            var azureId = "testAzureId1";
            using (var context = CreateContext())
            {
                context.AppUsers.Add(new AppUser { Name = "Test User 1", AzureAdObjectId = azureId, IsActive = true, LocationId = 1 });
                await context.SaveChangesAsync();
            }
            var result = await _userService.GetByAzureAdObjectIdAsync(azureId);
            Assert.IsNotNull(result);
            Assert.AreEqual("Test User 1", result.Name);
            Assert.AreEqual(azureId, result.AzureAdObjectId);
            Assert.IsTrue(result.IsActive);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_NewUser_CreatesAndReturnsUserDto()
        {
            var azureId = "newAzureUser";
            var userName = "New User Name";
            var claims = new List<Claim>
            {
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId),
                new Claim(ClaimTypes.Name, userName)
            };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);
            Assert.IsNotNull(resultDto);
            Assert.AreEqual(userName, resultDto.Name);
            Assert.AreEqual(azureId, resultDto.AzureAdObjectId);
            Assert.IsTrue(resultDto.IsActive); // Default IsActive
            Assert.AreEqual(1, resultDto.LocationId); // Default LocationId
            using (var context = CreateContext())
            {
                var dbUser = await context.AppUsers.FirstOrDefaultAsync(u => u.AzureAdObjectId == azureId);
                Assert.IsNotNull(dbUser);
                Assert.AreEqual(userName, dbUser.Name);
                Assert.AreEqual(1, dbUser.LocationId);
            }
        }
        #endregion

        // Helper methods for seeding RBAC entities
        private async Task<AppUser> SeedUserAsync(ApplicationDbContext context, string name, string azureId = null)
        {
            azureId ??= Guid.NewGuid().ToString();
            var user = new AppUser { Name = name, AzureAdObjectId = azureId, LocationId = 1, IsActive = true };
            context.AppUsers.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<Role> SeedRoleAsync(ApplicationDbContext context, string name)
        {
            var role = new Role { Name = name };
            context.Roles.Add(role);
            await context.SaveChangesAsync();
            return role;
        }

        private async Task<Permission> SeedPermissionAsync(ApplicationDbContext context, string name)
        {
            var permission = new Permission { Name = name };
            context.Permissions.Add(permission);
            await context.SaveChangesAsync();
            return permission;
        }

        // RBAC Tests
        [TestMethod]
        public async Task AssignRoleToUserAsync_ShouldCreateLink_WhenNotExists()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role = await SeedRoleAsync(context, "Role1");

            await _userService.AssignRoleToUserAsync(user.Id, role.Id);

            var linkExists = await context.UserRoles.AnyAsync(ur => ur.AppUserId == user.Id && ur.RoleId == role.Id);
            Assert.IsTrue(linkExists, "UserRole link should be created.");
        }

        [TestMethod]
        public async Task RemoveRoleFromUserAsync_ShouldRemoveLink_WhenExists()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role = await SeedRoleAsync(context, "Role1");
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();

            await _userService.RemoveRoleFromUserAsync(user.Id, role.Id);

            var linkExists = await context.UserRoles.AnyAsync(ur => ur.AppUserId == user.Id && ur.RoleId == role.Id);
            Assert.IsFalse(linkExists, "UserRole link should be removed.");
        }

        [TestMethod]
        public async Task GetRolesForUserAsync_ShouldReturnUserRoles_WithPermissions()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role1 = await SeedRoleAsync(context, "Role1");
            var perm1 = await SeedPermissionAsync(context, "Perm1");
            context.RolePermissions.Add(new RolePermission { RoleId = role1.Id, PermissionId = perm1.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role1.Id });
            await context.SaveChangesAsync();

            var result = await _userService.GetRolesForUserAsync(user.Id);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
            var userRole = result.First();
            Assert.AreEqual("Role1", userRole.Name);
            Assert.AreEqual(1, userRole.Permissions.Count);
            Assert.AreEqual("Perm1", userRole.Permissions.First().Name);
        }

        [TestMethod]
        public async Task UserHasRoleAsync_ShouldReturnTrue_WhenUserHasRole()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role = await SeedRoleAsync(context, "TargetRole");
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();

            var result = await _userService.UserHasRoleAsync(user.Id, "TargetRole");
            Assert.IsTrue(result, "User should have the role 'TargetRole'.");
        }

        [TestMethod]
        public async Task UserHasRoleAsync_ShouldReturnFalse_WhenUserDoesNotHaveRole()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            await SeedRoleAsync(context, "OtherRole"); // Seed some other role

            var result = await _userService.UserHasRoleAsync(user.Id, "TargetRole"); // Check for a role user doesn't have
            Assert.IsFalse(result, "User should not have the role 'TargetRole'.");
        }

        [TestMethod]
        public async Task UserHasPermissionAsync_ShouldReturnTrue_WhenUserHasPermissionViaRole()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role = await SeedRoleAsync(context, "Editor");
            var permission = await SeedPermissionAsync(context, "CanEditContent");
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();

            var result = await _userService.UserHasPermissionAsync(user.Id, "CanEditContent");
            Assert.IsTrue(result, "User should have permission 'CanEditContent'.");
        }

        [TestMethod]
        public async Task UserHasPermissionAsync_ShouldReturnFalse_WhenUserDoesNotHavePermission()
        {
            using var context = CreateContext();
            var user = await SeedUserAsync(context, "User1");
            var role = await SeedRoleAsync(context, "Viewer");
            var permission = await SeedPermissionAsync(context, "CanViewContent"); // User has this
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();

            await SeedPermissionAsync(context, "CanEditContent"); // This permission exists but is not assigned to user's role

            var result = await _userService.UserHasPermissionAsync(user.Id, "CanEditContent");
            Assert.IsFalse(result, "User should not have permission 'CanEditContent'.");
        }

        [TestMethod]
        public async Task GetAllAsync_ShouldPopulateUserRolesAndPermissions()
        {
            using var context = CreateContext();
            var user1 = await SeedUserAsync(context, "User One", "azure1");
            var roleAdmin = await SeedRoleAsync(context, "Admin");
            var permManage = await SeedPermissionAsync(context, "ManageAll");
            context.RolePermissions.Add(new RolePermission { RoleId = roleAdmin.Id, PermissionId = permManage.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user1.Id, RoleId = roleAdmin.Id });
            await context.SaveChangesAsync();

            var user2 = await SeedUserAsync(context, "User Two", "azure2"); // User with no roles

            var result = await _userService.GetAllAsync();

            Assert.AreEqual(2, result.Count());

            var user1Dto = result.FirstOrDefault(u => u.AzureAdObjectId == "azure1");
            Assert.IsNotNull(user1Dto);
            Assert.AreEqual(1, user1Dto.Roles.Count());
            var adminRoleDto = user1Dto.Roles.First();
            Assert.AreEqual("Admin", adminRoleDto.Name);
            Assert.AreEqual(1, adminRoleDto.Permissions.Count());
            Assert.AreEqual("ManageAll", adminRoleDto.Permissions.First().Name);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(user1Dto.Id), Times.Once);


            var user2Dto = result.FirstOrDefault(u => u.AzureAdObjectId == "azure2");
            Assert.IsNotNull(user2Dto);
            Assert.AreEqual(0, user2Dto.Roles.Count());
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(user2Dto.Id), Times.Once);
        }

        // --- New Tests for TotalMonthlyCost ---

        [TestMethod]
        public async Task GetAllAsync_PopulatesTotalMonthlyCost()
        {
            // Arrange
            decimal expectedCostUser1 = 1200m;
            decimal expectedCostUser2 = 1500m;

            AppUser user1Entity;
            AppUser user2Entity;

            using (var context = CreateContext())
            {
                user1Entity = await SeedUserAsync(context, "User1", "azure1");
                user2Entity = await SeedUserAsync(context, "User2", "azure2");
            }

            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(user1Entity.Id)).ReturnsAsync(expectedCostUser1);
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(user2Entity.Id)).ReturnsAsync(expectedCostUser2);

            // Act
            var result = await _userService.GetAllAsync();

            // Assert
            var user1Dto = result.FirstOrDefault(u => u.Id == user1Entity.Id);
            Assert.IsNotNull(user1Dto);
            Assert.AreEqual(expectedCostUser1, user1Dto.TotalMonthlyCost);

            var user2Dto = result.FirstOrDefault(u => u.Id == user2Entity.Id);
            Assert.IsNotNull(user2Dto);
            Assert.AreEqual(expectedCostUser2, user2Dto.TotalMonthlyCost);
        }

        [TestMethod]
        public async Task GetByIdsAsync_PopulatesTotalMonthlyCost()
        {
            // Arrange
            decimal expectedCost = 1300m;
            AppUser userEntity;
            using (var context = CreateContext())
            {
                userEntity = await SeedUserAsync(context, "UserByIds", "azureByIds");
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id)).ReturnsAsync(expectedCost);

            // Act
            var result = await _userService.GetByIdsAsync(new[] { userEntity.Id });

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(expectedCost, result.First().TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id), Times.Once);
        }

        [TestMethod]
        public async Task GetUserByIdAsync_PopulatesTotalMonthlyCost()
        {
            // Arrange
            decimal expectedCost = 1400m;
            AppUser userEntity;
            using (var context = CreateContext())
            {
                userEntity = await SeedUserAsync(context, "UserById", "azureById");
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id)).ReturnsAsync(expectedCost);

            // Act
            var resultDto = await _userService.GetUserByIdAsync(userEntity.Id);

            // Assert
            Assert.IsNotNull(resultDto);
            Assert.AreEqual(expectedCost, resultDto.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id), Times.Once);
        }

        [TestMethod]
        public async Task GetByAzureAdObjectIdAsync_PopulatesTotalMonthlyCost()
        {
            // Arrange
            var azureId = "testAzureIdForCost";
            decimal expectedCost = 1550m;
            AppUser userEntity;
            using (var context = CreateContext())
            {
                userEntity = await SeedUserAsync(context, "Test User Cost", azureId);
            }
             _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id)).ReturnsAsync(expectedCost);

            // Act
            var result = await _userService.GetByAzureAdObjectIdAsync(azureId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedCost, result.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(userEntity.Id), Times.Once);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_NewUser_PopulatesTotalMonthlyCost()
        {
            // Arrange
            var azureId = "newAzureUserForCost";
            var userName = "New User Cost Name";
            decimal expectedCost = 1600m;

            var claims = new List<Claim>
            {
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId),
                new Claim(ClaimTypes.Name, userName)
            };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

            // Setup mock to return specific cost for the user ID that will be created
            // This requires knowing/controlling the ID, or setting up a more generic It.IsAny<int>()
            // For simplicity, we'll use It.IsAny, assuming this test focuses on the call being made
            // and a specific ID would be verified in a separate test or if IDs were predictable.
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(It.IsAny<int>()))
                                 .ReturnsAsync(expectedCost);

            // Act
            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);

            // Assert
            Assert.IsNotNull(resultDto);
            Assert.AreEqual(expectedCost, resultDto.TotalMonthlyCost);
            // Verify it was called for the new user's ID
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(resultDto.Id), Times.Once);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_ExistingUser_PopulatesTotalMonthlyCost()
        {
            // Arrange
            var azureId = "existingUserForCost";
            var userName = "Existing User Cost Name";
            decimal expectedCost = 1700m;

            AppUser existingUser;
            using (var context = CreateContext())
            {
                existingUser = await SeedUserAsync(context, userName, azureId);
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCostAsync(existingUser.Id)).ReturnsAsync(expectedCost);

            var claims = new List<Claim>
            {
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId),
                new Claim(ClaimTypes.Name, userName) // Name could be different to test update path too
            };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

            // Act
            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);

            // Assert
            Assert.IsNotNull(resultDto);
            Assert.AreEqual(existingUser.Id, resultDto.Id);
            Assert.AreEqual(expectedCost, resultDto.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCostAsync(existingUser.Id), Times.Once);
        }

    }
}
