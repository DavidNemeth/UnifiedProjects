using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using UPortal.Dtos; // Required for AppUserDto if IAppUserService returns it, or direct model if used.
                    // AppUserService returns AppUserDto which has GrossMonthlyWage.

namespace UPortal.Services
{
    /// <summary>
    /// Implements financial calculations related to employees.
    /// </summary>
    public class FinancialService : IFinancialService
    {
        private readonly IAppUserService _appUserService;
        private readonly ILogger<FinancialService> _logger;

        // As per plan: SZOCHO is 13%. Total Cost = Gross Wage * 1.13.
        private const decimal EmployerSocialContributionTaxRate = 0.13m; // 13%

        /// <summary>
        /// Initializes a new instance of the <see cref="FinancialService"/> class.
        /// </summary>
        /// <param name="appUserService">The application user service to retrieve employee data.</param>
        /// <param name="logger">The logger for logging messages.</param>
        public FinancialService(IAppUserService appUserService, ILogger<FinancialService> logger)
        {
            _appUserService = appUserService ?? throw new ArgumentNullException(nameof(appUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<decimal> CalculateTotalMonthlyCostAsync(int employeeId)
        {
            _logger.LogInformation("Calculating total monthly cost for employee ID: {EmployeeId}", employeeId);

            // AppUserService.GetByIdsAsync returns a list. We need a method that returns a single user DTO by ID.
            // Let's assume AppUserService has or will have a method like GetByIdAsync(int userId) that returns AppUserDto.
            // For now, I will use GetByIdsAsync and take the first, assuming it's efficient enough or a direct GetByIdAsync exists/will be added.
            // A better approach would be to ensure IAppUserService has a GetByIdAsync method.
            // Based on current AppUserService, it does not have GetByIdAsync. It has GetByAzureAdObjectIdAsync.
            // This service should ideally operate on AppUserDto which contains the GrossMonthlyWage.
            // The controller will have the employeeId. We need to get AppUserDto from employeeId.
            // I will adjust the plan slightly: AppUserService needs a GetUserByIdAsync(int employeeId) method that returns AppUserDto.
            // For now, I'll proceed with a placeholder for fetching user data, assuming it will be resolved.

            AppUserDto? user = null;
            try
            {
                // This is a temporary stand-in. Ideally, IAppUserService should provide a method like:
                // user = await _appUserService.GetUserByIdAsync(employeeId);
                // For now, using GetByIdsAsync which is not ideal for a single user.
                var users = await _appUserService.GetByIdsAsync(new[] { employeeId });
                user = users.FirstOrDefault();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID {EmployeeId} for cost calculation.", employeeId);
                // Depending on policy, could rethrow or return 0. Plan says return 0.
                return 0m;
            }


            if (user == null)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} not found. Cannot calculate monthly cost.", employeeId);
                return 0m;
            }

            if (user.GrossMonthlyWage == null || user.GrossMonthlyWage <= 0)
            {
                _logger.LogWarning("Employee with ID {EmployeeId} has no GrossMonthlyWage set or it's zero. Monthly cost is 0.", employeeId);
                return 0m;
            }

            decimal grossWage = user.GrossMonthlyWage.Value;
            decimal employerSzocho = grossWage * EmployerSocialContributionTaxRate;
            decimal totalCost = grossWage + employerSzocho;

            _logger.LogInformation("Calculated total monthly cost for employee ID {EmployeeId}: Gross={GrossWage}, SZOCHO={SzochoAmount}, Total={TotalCost}",
                employeeId, grossWage, employerSzocho, totalCost);

            return totalCost;
        }
    }
}
