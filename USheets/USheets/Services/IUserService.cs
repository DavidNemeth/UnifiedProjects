using USheets.Dtos;

namespace USheets.Services
{
    public interface IUserService
    {
        Task<UserDto?> GetCurrentUserAsync();
    }
}
