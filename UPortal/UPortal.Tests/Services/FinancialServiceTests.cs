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
        private readonly Mock<ILogger<FinancialService>> _mockLogger;

        public FinancialServiceTests()
        {
            _mockLogger = new Mock<ILogger<FinancialService>>();
        }

        [Fact]
        public void CalculateTotalMonthlyCost_NoTaxes_ReturnsGrossWage()
        {
            // Arrange
            var grossWage = 1000m;
            var taxes = new List<CompanyTax>();
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(grossWage, result);
        }

        [Fact]
        public void CalculateTotalMonthlyCost_WithTaxes_ReturnsCorrectTotal()
        {
            // Arrange
            var grossWage = 1000m;
            var taxes = new List<CompanyTax>
            {
                new CompanyTax { Name = "Tax1", Rate = 0.10m }, // 100
                new CompanyTax { Name = "Tax2", Rate = 0.05m }  // 50
            };
            // Expected: 1000 + (1000 * 0.10) + (1000 * 0.05) = 1000 + 100 + 50 = 1150
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(1150m, result);
        }

        [Fact]
        public void CalculateTotalMonthlyCost_WithNegativeTaxRate_IgnoresNegativeRateTax()
        {
            // Arrange
            var grossWage = 1000m;
            var taxes = new List<CompanyTax>
            {
                new CompanyTax { Name = "Tax1", Rate = 0.10m },       // 100
                new CompanyTax { Name = "Tax2", Rate = -0.05m },      // Should be ignored
                new CompanyTax { Name = "Tax3", Rate = 0.02m }       // 20
            };
            // Expected: 1000 + (1000 * 0.10) + (1000 * 0.02) = 1000 + 100 + 20 = 1120
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(1120m, result);
             _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Company tax 'Tax2' has a negative rate -0.05. It will be ignored.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public void CalculateTotalMonthlyCost_GrossWageIsZero_ReturnsZero()
        {
            // Arrange
            var grossWage = 0m;
            var taxes = new List<CompanyTax> { new CompanyTax { Name = "Tax1", Rate = 0.10m } };
            // Expected: 0
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateTotalMonthlyCost_GrossWageIsNegative_ReturnsZero()
        {
            // Arrange
            var grossWage = -100m;
            var taxes = new List<CompanyTax> { new CompanyTax { Name = "Tax1", Rate = 0.10m } };
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(0m, result);
        }

        [Fact]
        public void CalculateTotalMonthlyCost_NullTaxes_ReturnsGrossWage()
        {
            // Arrange
            var grossWage = 1000m;
            List<CompanyTax> taxes = null;
            var service = new FinancialService(_mockLogger.Object);

            // Act
            var result = service.CalculateTotalMonthlyCost(grossWage, taxes);

            // Assert
            Assert.Equal(grossWage, result);
             _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No company taxes provided or applicable.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
