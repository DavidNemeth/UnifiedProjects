using USheets.Dtos;

namespace USheets.Services
{
    public interface IUserService
    {
        Task<AppUserDto?> GetCurrentUserAsync();

        /// <summary>
        /// Retrieves a dictionary of users by their unique identifiers.
        /// </summary>
        /// <param name="userIds">A collection of user IDs to retrieve.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains a Dictionary mapping the user ID to the <see cref="AppUserDto"/>.
        /// </returns>
        Task<Dictionary<int, AppUserDto>> GetUsersByIdsAsync(IEnumerable<int> userIds);
    }
}
