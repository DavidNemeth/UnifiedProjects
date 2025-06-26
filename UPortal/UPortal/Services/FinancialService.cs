using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using UPortal.Data;
using UPortal.Dtos; // AppUserDto is used here, but it's for fetching user details through AppUserService.

namespace UPortal.Services
{
    /// <summary>
    /// Implements financial calculations related to employees.
    /// </summary>
    public class FinancialService : IFinancialService
    {
        private readonly IAppUserService _appUserService; // To get user's gross wage
        private readonly ApplicationDbContext _dbContext; // To get company taxes
        private readonly ILogger<FinancialService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FinancialService"/> class.
        /// </summary>
        /// <param name="appUserService">The application user service to retrieve employee data.</param>
        /// <param name="dbContext">The database context for accessing company tax data.</param>
        /// <param name="logger">The logger for logging messages.</param>
        public FinancialService(IAppUserService appUserService, ApplicationDbContext dbContext, ILogger<FinancialService> logger)
        {
            _appUserService = appUserService ?? throw new ArgumentNullException(nameof(appUserService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<decimal> CalculateTotalMonthlyCostAsync(int employeeId)
        {
            _logger.LogInformation("Calculating total monthly cost for employee ID: {EmployeeId}", employeeId);

            AppUserDto? user = null;
            try
            {
                // Assuming GetByIdsAsync is the way to get user details for now.
                // This might be refactored in AppUserService later if a more direct GetUserByIdAsync(id) is added.
                var users = await _appUserService.GetByIdsAsync(new[] { employeeId });
                user = users.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID {EmployeeId} for cost calculation.", employeeId);
                return 0m; // Return 0 as per previous logic if user retrieval fails
            }

            if (user == null)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} not found. Cannot calculate monthly cost.", employeeId);
                return 0m;
            }

            if (user.GrossMonthlyWage == null || user.GrossMonthlyWage <= 0)
            {
                _logger.LogInformation("Employee with ID {EmployeeId} has no GrossMonthlyWage set or it's zero/negative. Monthly cost is 0.", employeeId);
                return 0m;
            }

            decimal grossWage = user.GrossMonthlyWage.Value;
            decimal totalTaxesAmount = 0;

            try
            {
                var companyTaxes = await _dbContext.CompanyTaxes.ToListAsync();
                if (companyTaxes.Any())
                {
                    foreach (var tax in companyTaxes)
                    {
                        totalTaxesAmount += grossWage * tax.Rate;
                    }
                    _logger.LogInformation("Applied {NumTaxes} company taxes for employee ID {EmployeeId}. Total tax amount: {TotalTaxesAmount}",
                                           companyTaxes.Count, employeeId, totalTaxesAmount);
                }
                else
                {
                    _logger.LogInformation("No company taxes found in the database. Only gross wage will be considered for employee ID {EmployeeId}.", employeeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company taxes for employee ID {EmployeeId}. Cost calculation will proceed without company taxes.", employeeId);
                // Continue calculation with grossWage only if taxes cannot be fetched.
            }

            decimal totalCost = grossWage + totalTaxesAmount;

            _logger.LogInformation("Calculated total monthly cost for employee ID {EmployeeId}: Gross={GrossWage}, Taxes={TotalTaxesAmount}, Total={TotalCost}",
                employeeId, grossWage, totalTaxesAmount, totalCost);

            return totalCost;
        }
    }
}
