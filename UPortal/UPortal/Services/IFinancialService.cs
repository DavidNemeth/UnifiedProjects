using System.Threading.Tasks;

namespace UPortal.Services
{
    /// <summary>
    /// Defines operations for financial calculations related to employees.
    /// </summary>
    public interface IFinancialService
    {
        /// <summary>
        /// Calculates the total monthly cost of an employee to the company.
        /// This includes gross wage and any applicable employer-paid taxes.
        /// </summary>
        /// <param name="employeeId">The ID of the employee.</param>
        /// <returns>The total calculated monthly cost. Returns 0 if the employee or their gross wage is not found.</returns>
        Task<decimal> CalculateTotalMonthlyCostAsync(int employeeId);
    }
}
