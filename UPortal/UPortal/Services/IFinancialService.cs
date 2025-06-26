using UPortal.Dtos;

namespace UPortal.Services
{
    /// <summary>
    /// Defines operations for financial calculations related to employees.
    /// </summary>
    public interface IFinancialService
    {
        /// <summary>
        /// Calculates the total monthly cost based on a gross wage and a list of applicable company taxes.
        /// This includes the gross wage plus all calculated taxes.
        /// </summary>
        /// <param name="grossWage">The gross monthly wage of the employee.</param>
        /// <param name="allCompanyTaxes">An enumerable of all company taxes to be applied.</param>
        /// <returns>The total calculated monthly cost. Returns the original gross wage if no taxes are applicable or if gross wage is non-positive.</returns>
        decimal CalculateTotalMonthlyCost(decimal grossWage, IEnumerable<CompanyTaxDto> allCompanyTaxes);
    }
}
