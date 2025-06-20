namespace USheets.Dtos
{
    public class AppUserDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the user account is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the Azure Active Directory Object ID for the user.
        /// </summary>
        public string AzureAdObjectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the user's location.
        /// </summary>
        public int LocationId { get; set; }

        /// <summary>
        /// Gets or sets the name of the user's location.
        /// </summary>
        public string LocationName { get; set; } = string.Empty;


        /// <summary>   
        /// Gets or sets the list of role names assigned to the user.
        /// </summary>
        public List<string> RoleNames { get; set; } = new List<string>();
    }
}
