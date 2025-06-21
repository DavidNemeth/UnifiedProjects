using System.Collections.Generic;
using System.Threading.Tasks;
using UPortal.Data.Models; // For SeniorityLevelEnum
using UPortal.Dtos;

namespace UPortal.Services
{
    /// <summary>
    /// Defines operations for managing seniority rates.
    /// </summary>
    public interface ISeniorityRateService
    {
        /// <summary>
        /// Retrieves all seniority rates.
        /// </summary>
        /// <returns>A collection of <see cref="SeniorityRateDto"/>.</returns>
        Task<IEnumerable<SeniorityRateDto>> GetAllAsync();

        /// <summary>
        /// Retrieves a specific seniority rate by its level.
        /// </summary>
        /// <param name="level">The seniority level.</param>
        /// <returns>The <see cref="SeniorityRateDto"/> if found; otherwise, null.</returns>
        Task<SeniorityRateDto?> GetByLevelAsync(SeniorityLevelEnum level);

        /// <summary>
        /// Creates a new seniority rate.
        /// </summary>
        /// <param name="dto">The data transfer object containing the details for the new seniority rate.</param>
        /// <returns>The created <see cref="SeniorityRateDto"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if a rate for the given level already exists or if the level string is invalid.</exception>
        /// <exception cref="DbUpdateException">Thrown if there is an issue saving to the database.</exception>
        Task<SeniorityRateDto> CreateAsync(SeniorityRateDto dto);

        /// <summary>
        /// Updates an existing seniority rate.
        /// </summary>
        /// <param name="level">The seniority level of the rate to update.</param>
        /// <param name="dto">The data transfer object containing the updated details. The ID in the DTO is ignored; level is used for lookup.</param>
        /// <returns>The updated <see cref="SeniorityRateDto"/> if found and updated; otherwise, null.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no rate for the given level is found.</exception>
        /// <exception cref="DbUpdateException">Thrown if there is an issue saving to the database.</exception>
        Task<SeniorityRateDto?> UpdateAsync(SeniorityLevelEnum level, SeniorityRateDto dto);

        /// <summary>
        /// Deletes a seniority rate by its level.
        /// </summary>
        /// <param name="level">The seniority level of the rate to delete.</param>
        /// <returns>True if the rate was found and deleted; otherwise, false.</returns>
        Task<bool> DeleteAsync(SeniorityLevelEnum level);
    }
}
