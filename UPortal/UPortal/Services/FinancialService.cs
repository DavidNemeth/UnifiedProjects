using UPortal.Dtos;

namespace UPortal.Services
{
    /// <summary>
    /// Implements financial calculations related to employees.
    /// </summary>
    public class FinancialService : IFinancialService
    {
        private readonly ILogger<FinancialService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FinancialService"/> class.
        /// </summary>
        /// <param name="logger">The logger for logging messages.</param>
        public FinancialService(ILogger<FinancialService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public decimal CalculateTotalMonthlyCost(decimal grossWage, IEnumerable<CompanyTaxDto> allCompanyTaxes)
        {
            if (grossWage <= 0)
            {
                _logger.LogInformation("Gross wage is {GrossWage} (zero or negative). Total monthly cost will be considered 0.", grossWage);
                return 0m; // Or return grossWage if that's the desired behavior for 0 wage. Task implies cost is additive.
            }

            decimal totalTaxesAmount = 0;

            if (allCompanyTaxes != null && allCompanyTaxes.Any())
            {
                foreach (var tax in allCompanyTaxes)
                {
                    if (tax.Rate < 0)
                    {
                        _logger.LogWarning("Company tax '{TaxName}' has a negative rate {TaxRate}. It will be ignored.", tax.Name, tax.Rate);
                        continue;
                    }
                    totalTaxesAmount += grossWage * tax.Rate;
                }
                _logger.LogInformation("Calculated total taxes amount: {TotalTaxesAmount} for gross wage {GrossWage} based on {NumTaxes} company taxes.",
                                       totalTaxesAmount, grossWage, allCompanyTaxes.Count());
            }
            else
            {
                _logger.LogInformation("No company taxes provided or applicable. Total taxes amount is 0 for gross wage {GrossWage}.", grossWage);
            }

            decimal totalCost = grossWage + totalTaxesAmount;

            _logger.LogInformation("Calculated total monthly cost: Gross={GrossWage}, Taxes={TotalTaxesAmount}, Total={TotalCost}",
                                   grossWage, totalTaxesAmount, totalCost);

            return totalCost;
        }
    }
}
