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
using System.Threading;

namespace UPortal.Tests.Services
{
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
        private Mock<IFinancialService> _mockFinancialService;
        private AppUserService _userService;

        [TestInitialize]
        public void Initialize()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new ApplicationDbContext(_options))
            {
                context.Database.EnsureCreated();
                if (!context.Locations.Any(l => l.Id == 1))
                {
                    context.Locations.Add(new Location { Id = 1, Name = "Default Location" });
                }
                context.CompanyTaxes.RemoveRange(context.CompanyTaxes); // Clear taxes
                context.AppUsers.RemoveRange(context.AppUsers);
                context.UserRoles.RemoveRange(context.UserRoles);
                context.Roles.RemoveRange(context.Roles);
                context.Permissions.RemoveRange(context.Permissions);
                context.RolePermissions.RemoveRange(context.RolePermissions);
                context.SaveChanges();
            }

            _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            _mockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ApplicationDbContext(_options));
            _mockDbContextFactory.Setup(f => f.CreateDbContext())
                .Returns(() => new ApplicationDbContext(_options));

            _mockLogger = new Mock<ILogger<AppUserService>>();
            _mockFinancialService = new Mock<IFinancialService>();

            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(It.IsAny<decimal>(), It.IsAny<IEnumerable<CompanyTax>>()))
                .Returns(0m); // Default setup

            _userService = new AppUserService(_mockDbContextFactory.Object, _mockLogger.Object, _mockFinancialService.Object);
        }

        private ApplicationDbContext CreateContext() => new ApplicationDbContext(_options);

        // Helper to seed user with optional wage
        private async Task<AppUser> SeedUserAsync(ApplicationDbContext context, string name, string azureId = null, decimal? wage = null)
        {
            azureId ??= Guid.NewGuid().ToString();
            var user = new AppUser { Name = name, AzureAdObjectId = azureId, LocationId = 1, IsActive = true, GrossMonthlyWage = wage };
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

        private async Task SeedCompanyTaxesAsync(ApplicationDbContext context, List<CompanyTax> taxes)
        {
            await context.CompanyTaxes.AddRangeAsync(taxes);
            await context.SaveChangesAsync();
        }


        [TestMethod]
        public async Task GetAllAsync_PopulatesTotalMonthlyCost_WhenWageExists()
        {
            // Arrange
            decimal user1Wage = 1000m;
            decimal user2Wage = 0; // No wage, cost should be 0
            decimal expectedCostUser1 = 1130m; // e.g. 1000 * 1.13

            AppUser user1Entity, user2Entity, user3EntityWithNullWage;
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes);
                user1Entity = await SeedUserAsync(context, "User1", "azure1", user1Wage);
                user2Entity = await SeedUserAsync(context, "User2", "azure2", user2Wage);
                user3EntityWithNullWage = await SeedUserAsync(context, "User3", "azure3", null);
            }

            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(user1Wage, It.IsAny<IEnumerable<CompanyTax>>()))
                .Returns(expectedCostUser1);

            // Act
            var result = await _userService.GetAllAsync();

            // Assert
            var user1Dto = result.FirstOrDefault(u => u.Id == user1Entity.Id);
            Assert.IsNotNull(user1Dto);
            Assert.AreEqual(expectedCostUser1, user1Dto.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(user1Wage, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);

            var user2Dto = result.FirstOrDefault(u => u.Id == user2Entity.Id);
            Assert.IsNotNull(user2Dto);
            Assert.AreEqual(0m, user2Dto.TotalMonthlyCost); // Wage is 0

            var user3Dto = result.FirstOrDefault(u => u.Id == user3EntityWithNullWage.Id);
            Assert.IsNotNull(user3Dto);
            Assert.AreEqual(0m, user3Dto.TotalMonthlyCost); // Wage is null
        }


        [TestMethod]
        public async Task GetByIdsAsync_PopulatesTotalMonthlyCost_WhenWageExists()
        {
            decimal userWage = 1100m;
            decimal expectedCost = 1243m; // e.g. 1100 * 1.13
            AppUser userEntity;
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes);
                userEntity = await SeedUserAsync(context, "UserByIds", "azureByIds", userWage);
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(userWage, It.IsAny<IEnumerable<CompanyTax>>())).Returns(expectedCost);

            var result = await _userService.GetByIdsAsync(new[] { userEntity.Id });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(expectedCost, result.First().TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(userWage, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);
        }


        [TestMethod]
        public async Task GetUserByIdAsync_PopulatesTotalMonthlyCost_WhenWageExists()
        {
            decimal userWage = 1150m;
            decimal expectedCost = 1299.5m; // e.g. 1150 * 1.13
            AppUser userEntity;
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes);
                userEntity = await SeedUserAsync(context, "UserById", "azureById", userWage);
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(userWage, It.IsAny<IEnumerable<CompanyTax>>())).Returns(expectedCost);

            var resultDto = await _userService.GetUserByIdAsync(userEntity.Id);

            Assert.IsNotNull(resultDto);
            Assert.AreEqual(expectedCost, resultDto.TotalMonthlyCost);
             _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(userWage, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);
        }

        [TestMethod]
        public async Task GetByAzureAdObjectIdAsync_PopulatesTotalMonthlyCost_WhenWageExists()
        {
            var azureId = "testAzureIdForCost";
            decimal userWage = 1250m;
            decimal expectedCost = 1412.5m; // e.g. 1250 * 1.13
            AppUser userEntity;
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes);
                userEntity = await SeedUserAsync(context, "Test User Cost", azureId, userWage);
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(userWage, It.IsAny<IEnumerable<CompanyTax>>())).Returns(expectedCost);

            var result = await _userService.GetByAzureAdObjectIdAsync(azureId);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedCost, result.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(userWage, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_NewUser_SetsZeroCost_AsNoWageInitially()
        {
            var azureId = "newAzureUserForCost";
            var userName = "New User Cost Name";
            var claims = new List<Claim> { new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId), new Claim(ClaimTypes.Name, userName) };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes); // Taxes exist
            }

            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);

            Assert.IsNotNull(resultDto);
            Assert.IsNull(resultDto.GrossMonthlyWage);
            Assert.AreEqual(0m, resultDto.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(It.IsAny<decimal>(), It.IsAny<IEnumerable<CompanyTax>>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_ExistingUserWithWage_PopulatesTotalMonthlyCost()
        {
            var azureId = "existingUserForCostUpdate";
            var userName = "Existing User Cost Name Update";
            decimal existingUserWage = 1350m;
            decimal expectedCost = 1525.5m; // 1350 * 1.13
            AppUser existingUser;
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };

            using (var context = CreateContext())
            {
                await SeedCompanyTaxesAsync(context, taxes);
                existingUser = await SeedUserAsync(context, userName, azureId, existingUserWage);
            }
            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(existingUserWage, It.IsAny<IEnumerable<CompanyTax>>())).Returns(expectedCost);

            var claims = new List<Claim> { new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId), new Claim(ClaimTypes.Name, userName) };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));

            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);

            Assert.IsNotNull(resultDto);
            Assert.AreEqual(existingUser.Id, resultDto.Id);
            Assert.AreEqual(existingUserWage, resultDto.GrossMonthlyWage);
            Assert.AreEqual(expectedCost, resultDto.TotalMonthlyCost);
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(existingUserWage, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);
        }


        [TestMethod]
        public async Task GetAllAsync_ShouldPopulateUserRolesAndPermissions_AndCallFinancialServiceForWagedUsers()
        {
            using var context = CreateContext();
            var taxes = new List<CompanyTax> { new CompanyTax { Id = 1, Name = "SZOCHO", Rate = 0.13m } };
            await SeedCompanyTaxesAsync(context, taxes);

            var user1 = await SeedUserAsync(context, "User One", "azure1", 1000m); // Has wage
            var roleAdmin = await SeedRoleAsync(context, "Admin");
            var permManage = await SeedPermissionAsync(context, "ManageAll");
            context.RolePermissions.Add(new RolePermission { RoleId = roleAdmin.Id, PermissionId = permManage.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user1.Id, RoleId = roleAdmin.Id });

            var user2 = await SeedUserAsync(context, "User Two", "azure2"); // No wage

            await context.SaveChangesAsync();

            _mockFinancialService.Setup(fs => fs.CalculateTotalMonthlyCost(1000m, It.IsAny<IEnumerable<CompanyTax>>())).Returns(1130m);

            var result = await _userService.GetAllAsync();

            Assert.AreEqual(2, result.Count());

            var user1Dto = result.FirstOrDefault(u => u.AzureAdObjectId == "azure1");
            Assert.IsNotNull(user1Dto);
            Assert.AreEqual(1, user1Dto.Roles.Count());
            Assert.AreEqual(1130m, user1Dto.TotalMonthlyCost);
             _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(1000m, It.Is<IEnumerable<CompanyTax>>(t => t.Any(ct => ct.Rate == 0.13m))), Times.Once);


            var user2Dto = result.FirstOrDefault(u => u.AzureAdObjectId == "azure2");
            Assert.IsNotNull(user2Dto);
            Assert.AreEqual(0, user2Dto.Roles.Count());
            Assert.AreEqual(0m, user2Dto.TotalMonthlyCost); // No wage, so cost is 0
            // Ensure it's not called for user2 because wage is null
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(It.Is<decimal>(d => d != 1000m), It.IsAny<IEnumerable<CompanyTax>>()), Times.Never);
        }

        // --- Original tests (should still pass, some might need minor adjustments if they implicitly relied on old financial calc) ---
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
            var exception = await Assert.ThrowsExceptionAsync<KeyNotFoundException>(
                () => _userService.UpdateAppUserAsync(nonExistentUserId, userToUpdateDto));
            Assert.IsTrue(exception.Message.Contains($"User with ID {nonExistentUserId} not found."));
            _mockLogger.VerifyLogging(LogLevel.Error, $"User with ID {nonExistentUserId} not found for update.", Times.Once());
        }


        [TestMethod]
        public async Task GetByAzureAdObjectIdAsync_UserExists_ReturnsUserDto_AndZeroCostIfNoWage()
        {
            var azureId = "testAzureId1";
            AppUser userEntity;
            using (var context = CreateContext())
            {
                userEntity = await SeedUserAsync(context, "Test User 1", azureId, null); // No wage
                 await SeedCompanyTaxesAsync(context, new List<CompanyTax> { new CompanyTax { Name="Tax", Rate=0.1m}});
            }
            var result = await _userService.GetByAzureAdObjectIdAsync(azureId);
            Assert.IsNotNull(result);
            Assert.AreEqual("Test User 1", result.Name);
            Assert.AreEqual(0m, result.TotalMonthlyCost); // No wage = 0 cost
            _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(It.IsAny<decimal>(), It.IsAny<IEnumerable<CompanyTax>>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateOrUpdateUserFromAzureAdAsync_NewUser_CreatesAndReturnsUserDto_WithZeroCost()
        {
            var azureId = "newAzureUser";
            var userName = "New User Name";
            var claims = new List<Claim>
            {
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", azureId),
                new Claim(ClaimTypes.Name, userName)
            };
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
             using (var context = CreateContext())
            {
                 await SeedCompanyTaxesAsync(context, new List<CompanyTax> { new CompanyTax { Name="Tax", Rate=0.1m}});
            }
            var resultDto = await _userService.CreateOrUpdateUserFromAzureAdAsync(userPrincipal);
            Assert.IsNotNull(resultDto);
            Assert.AreEqual(userName, resultDto.Name);
            Assert.AreEqual(0m, resultDto.TotalMonthlyCost); // New user, no wage by default
             _mockFinancialService.Verify(fs => fs.CalculateTotalMonthlyCost(It.IsAny<decimal>(), It.IsAny<IEnumerable<CompanyTax>>()), Times.Never);

            using (var context = CreateContext())
            {
                var dbUser = await context.AppUsers.FirstOrDefaultAsync(u => u.AzureAdObjectId == azureId);
                Assert.IsNotNull(dbUser);
                Assert.IsNull(dbUser.GrossMonthlyWage);
            }
        }

        // RBAC Tests (should largely be unaffected)
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
            await SeedRoleAsync(context, "OtherRole");
            var result = await _userService.UserHasRoleAsync(user.Id, "TargetRole");
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
            var permission = await SeedPermissionAsync(context, "CanViewContent");
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            context.UserRoles.Add(new UserRole { AppUserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();
            await SeedPermissionAsync(context, "CanEditContent");
            var result = await _userService.UserHasPermissionAsync(user.Id, "CanEditContent");
            Assert.IsFalse(result, "User should not have permission 'CanEditContent'.");
        }
    }
}
