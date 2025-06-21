using System.ComponentModel.DataAnnotations;
using UPortal.Data.Models; // Required for SeniorityLevelEnum, though it will be string here

namespace UPortal.Dtos
{
    /// <summary>
    /// Data Transfer Object for Seniority Rate information.
    /// </summary>
    public class SeniorityRateDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the seniority rate.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the seniority level.
        /// This is represented as a string (e.g., "Junior", "Senior").
        /// </summary>
        [Required]
        public string Level { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the daily consulting rate for this seniority level.
        /// </summary>
        [Required]
        [Range(0, (double)decimal.MaxValue)]
        public decimal DailyRate { get; set; }
    }
}
