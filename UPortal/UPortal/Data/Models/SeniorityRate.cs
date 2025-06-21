using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UPortal.Data.Models
{
    /// <summary>
    /// Stores the daily consulting rate for each seniority level.
    /// Ensures that each seniority level has a unique rate defined.
    /// </summary>
    [Index(nameof(Level), IsUnique = true)]
    public class SeniorityRate
    {
        /// <summary>
        /// Gets or sets the unique identifier for the seniority rate.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the seniority level.
        /// Each level must be unique within this table.
        /// </summary>
        [Required]
        public SeniorityLevelEnum Level { get; set; }

        /// <summary>
        /// Gets or sets the daily consulting rate for this seniority level.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal DailyRate { get; set; }
    }
}
