using Freeway.Domain.Entities;

namespace Freeway.Domain.Interfaces;

public interface IAuthService
{
    Task<User?> ValidateCredentialsAsync(string email, string password);
    Task<User> CreateUserAsync(string email, string password, string? name = null, bool isAdmin = false);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task<List<User>> GetAllUsersAsync();
    Task UpdateLastLoginAsync(Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<User?> UpdateUserAsync(Guid userId, string? name, string? email, bool? isActive, bool? isAdmin);
    Task<bool> DeleteUserAsync(Guid userId);
    Task SeedDefaultAdminAsync();
    string GenerateJwtToken(User user);
    Guid? ValidateJwtToken(string token);
}
