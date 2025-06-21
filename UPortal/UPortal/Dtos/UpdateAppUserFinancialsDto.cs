using System.ComponentModel.DataAnnotations;
using UPortal.Data.Models; // Required for SeniorityLevelEnum

namespace UPortal.Dtos
{
    /// <summary>
    /// Data Transfer Object for updating an AppUser's financial information.
    /// </summary>
    public class UpdateAppUserFinancialsDto
    {
        /// <summary>
        /// Gets or sets the user's gross monthly wage.
        /// This field is nullable. A null value typically means no change or not set.
        /// </summary>
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Gross monthly wage must be a non-negative value.")]
        public decimal? GrossMonthlyWage { get; set; }

        /// <summary>
        /// Gets or sets the user's seniority level.
        /// This field is nullable. A null value typically means no change or not set.
        /// </summary>
        public SeniorityLevelEnum? SeniorityLevel { get; set; }
    }
}
