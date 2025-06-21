﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UPortal.Data.Models
{
    /// <summary>
    /// Represents an application user, including their profile, roles, and financial information.
    /// </summary>
    public class AppUser
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the Azure Active Directory Object ID for the user.
        /// This is used to link the application user to their Azure AD identity.
        /// </summary>
        [Required]
        public string AzureAdObjectId { get; set; }

        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user account is active. Defaults to true.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the foreign key for the user's location.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property for the user's location.
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// Gets or sets the gross monthly wage for the employee.
        /// This field is nullable and stores the wage amount.
        /// </summary>
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? GrossMonthlyWage { get; set; }

        /// <summary>
        /// Gets or sets the seniority level of the employee.
        /// This field is nullable and uses the <see cref="SeniorityLevelEnum"/>.
        /// </summary>
        public SeniorityLevelEnum? SeniorityLevel { get; set; }

        /// <summary>
        /// Gets or sets the collection of machines assigned to this user.
        /// Initialized to an empty list to prevent null reference exceptions.
        /// </summary>
        public ICollection<Machine> Machines { get; set; } = new List<Machine>();

        /// <summary>
        /// Gets or sets the collection of roles assigned to this user.
        /// Initialized to an empty list to prevent null reference exceptions.
        /// </summary>
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
