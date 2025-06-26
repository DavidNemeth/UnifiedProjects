using UPortal.Dtos;

namespace UPortal.Services
{
    /// <summary>
    /// Defines the contract for services that manage company tax information.
    /// </summary>
    public interface ICompanyTaxService
    {
        /// <summary>
        /// Retrieves all company taxes asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result contains a collection of all company taxes.</returns>
        Task<IEnumerable<CompanyTaxDto>> GetAllAsync();

        /// <summary>
        /// Retrieves a specific company tax by its unique identifier asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the company tax.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result contains the company tax DTO if found; otherwise, null.</returns>
        Task<CompanyTaxDto?> GetByIdAsync(int id);

        /// <summary>
        /// Creates a new company tax asynchronously.
        /// </summary>
        /// <param name="companyTaxDto">The data transfer object containing the details of the company tax to create.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result contains the created company tax DTO, including its new ID.</returns>
        Task<CompanyTaxDto> CreateAsync(CompanyTaxDto companyTaxDto);

        /// <summary>
        /// Updates an existing company tax asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the company tax to update.</param>
        /// <param name="companyTaxDto">The data transfer object containing the updated details.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result is true if the update was successful; otherwise, false.</returns>
        Task<bool> UpdateAsync(int id, CompanyTaxDto companyTaxDto);

        /// <summary>
        /// Deletes a company tax by its unique identifier asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the company tax to delete.</param>
        /// <returns>A task that represents the asynchronous operation. 
        /// The task result is true if the deletion was successful; otherwise, false.</returns>
        Task<bool> DeleteAsync(int id);
    }
}
