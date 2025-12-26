using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Freeway.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", email);
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password for user: {Email}", email);
            return null;
        }

        return user;
    }

    public async Task<User> CreateUserAsync(string email, string password, string? name = null, bool isAdmin = false)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            Name = name,
            IsAdmin = isAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new user: {Email} (Admin: {IsAdmin})", email, isAdmin);
        return user;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.OrderBy(u => u.Email).ToListAsync();
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password changed for user: {Email}", user.Email);
        return true;
    }

    public async Task<User?> UpdateUserAsync(Guid userId, string? name, string? email, bool? isActive, bool? isAdmin)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        if (name != null)
            user.Name = name;
        if (email != null)
            user.Email = email;
        if (isActive.HasValue)
            user.IsActive = isActive.Value;
        if (isAdmin.HasValue)
            user.IsAdmin = isAdmin.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated user: {Email}", user.Email);
        return user;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted user: {Email}", user.Email);
        return true;
    }

    public async Task SeedDefaultAdminAsync()
    {
        var adminEmail = _configuration["DEFAULT_ADMIN_EMAIL"];
        var adminPassword = _configuration["DEFAULT_ADMIN_PASSWORD"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            _logger.LogWarning("DEFAULT_ADMIN_EMAIL or DEFAULT_ADMIN_PASSWORD not configured, skipping admin seeding");
            return;
        }

        if (await EmailExistsAsync(adminEmail))
        {
            _logger.LogInformation("Admin user already exists: {Email}", adminEmail);
            return;
        }

        await CreateUserAsync(adminEmail, adminPassword, "Admin", isAdmin: true);
        _logger.LogInformation("Seeded default admin user: {Email}", adminEmail);
    }

    public string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET is not configured");
        var expiryHours = int.Parse(_configuration["JWT_EXPIRY_HOURS"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name ?? user.Email),
            new Claim("is_admin", user.IsAdmin.ToString().ToLower()),
            new Claim("auth_type", "user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "freeway",
            audience: "freeway-web",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateJwtToken(string token)
    {
        try
        {
            var jwtSecret = _configuration["JWT_SECRET"];
            if (string.IsNullOrEmpty(jwtSecret))
                return null;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "freeway",
                ValidAudience = "freeway-web",
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return userId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("JWT validation failed: {Message}", ex.Message);
            return null;
        }
    }
}
