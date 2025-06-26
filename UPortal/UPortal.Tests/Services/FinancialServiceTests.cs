using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using UPortal.Services;
using UPortal.Dtos;
using UPortal.Data;
using UPortal.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace UPortal.Tests.Services
{
    public class FinancialServiceTests
    {
        private readonly Mock<IAppUserService> _mockAppUserService;
        private readonly Mock<ILogger<FinancialService>> _mockLogger;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private ApplicationDbContext _dbContext; // In-memory DB context

        public FinancialServiceTests()
        {
            _mockAppUserService = new Mock<IAppUserService>();
            _mockLogger = new Mock<ILogger<FinancialService>>();

            // Setup InMemory database
            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
                .Options;

            _dbContext = new ApplicationDbContext(_dbContextOptions);
        }

        private void SeedCompanyTaxes(List<CompanyTax> taxes)
        {
            _dbContext.CompanyTaxes.AddRange(taxes);
            _dbContext.SaveChanges();
        }

        private void ClearCompanyTaxes()
        {
            _dbContext.CompanyTaxes.RemoveRange(_dbContext.CompanyTaxes.ToList());
            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserFound_NoTaxes_ReturnsGrossWage()
        {
            // Arrange
            var employeeId = 1;
            var grossWage = 1000m;
            var userDto = new AppUserDto { Id = employeeId, GrossMonthlyWage = grossWage };
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(employeeId))))
                .ReturnsAsync(new List<AppUserDto> { userDto });

            ClearCompanyTaxes(); // Ensure no taxes exist

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(grossWage, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserFound_WithTaxes_ReturnsCorrectTotal()
        {
            // Arrange
            var employeeId = 1;
            var grossWage = 1000m;
            var userDto = new AppUserDto { Id = employeeId, GrossMonthlyWage = grossWage };
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(employeeId))))
                .ReturnsAsync(new List<AppUserDto> { userDto });

            ClearCompanyTaxes();
            SeedCompanyTaxes(new List<CompanyTax>
            {
                new CompanyTax { Name = "Tax1", Rate = 0.10m }, // 100
                new CompanyTax { Name = "Tax2", Rate = 0.05m }  // 50
            });
            // Expected: 1000 + (1000 * 0.10) + (1000 * 0.05) = 1000 + 100 + 50 = 1150

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(1150m, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserNotFound_ReturnsZero()
        {
            // Arrange
            var employeeId = 1;
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new List<AppUserDto>()); // Empty list means user not found

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserFound_GrossWageIsNull_ReturnsZero()
        {
            // Arrange
            var employeeId = 1;
            var userDto = new AppUserDto { Id = employeeId, GrossMonthlyWage = null };
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(employeeId))))
                .ReturnsAsync(new List<AppUserDto> { userDto });

            ClearCompanyTaxes();
            SeedCompanyTaxes(new List<CompanyTax> { new CompanyTax { Name = "Tax1", Rate = 0.10m }});

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserFound_GrossWageIsZero_ReturnsZero()
        {
            // Arrange
            var employeeId = 1;
            var userDto = new AppUserDto { Id = employeeId, GrossMonthlyWage = 0m };
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(employeeId))))
                .ReturnsAsync(new List<AppUserDto> { userDto });

            ClearCompanyTaxes();
            SeedCompanyTaxes(new List<CompanyTax> { new CompanyTax { Name = "Tax1", Rate = 0.10m }});
            // Expected: 0 + (0 * 0.10) = 0

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_UserFound_GrossWageIsNegative_ReturnsZero()
        {
            // Arrange
            var employeeId = 1;
            var userDto = new AppUserDto { Id = employeeId, GrossMonthlyWage = -100m };
             _mockAppUserService.Setup(s => s.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(employeeId))))
                .ReturnsAsync(new List<AppUserDto> { userDto });

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public async Task CalculateTotalMonthlyCostAsync_AppUserServiceThrowsException_ReturnsZero()
        {
            // Arrange
            var employeeId = 1;
            _mockAppUserService.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
                .ThrowsAsync(new System.InvalidOperationException("Simulated service error"));

            var service = new FinancialService(_mockAppUserService.Object, _dbContext, _mockLogger.Object);

            // Act
            var result = await service.CalculateTotalMonthlyCostAsync(employeeId);

            // Assert
            Assert.Equal(0m, result);
            // Verify logger was called with error for this scenario
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving user")),
                    It.IsAny<System.InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Note: Testing DbContext throwing an exception when fetching CompanyTaxes
        // is harder with InMemory provider as it rarely fails in simple reads.
        // The service logic already logs and continues if taxes can't be fetched, resulting in cost = grosswage.
        // We can test this by ensuring the DB is empty of taxes, which is covered by CalculateTotalMonthlyCostAsync_UserFound_NoTaxes_ReturnsGrossWage
    }
}
