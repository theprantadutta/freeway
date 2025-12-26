using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IAuthService
{
    Task<User?> ValidateCredentialsAsync(string username, string password);
    Task<User> CreateUserAsync(string username, string password, string? email = null);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> UsernameExistsAsync(string username);
    Task<int> GetUserCountAsync();
    Task UpdateLastLoginAsync(Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    string GenerateJwtToken(User user);
    Guid? ValidateJwtToken(string token);
}
