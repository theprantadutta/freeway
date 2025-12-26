using System.Security.Claims;
using Freeway.Api.Attributes;
using Freeway.Application.DTOs;
using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Freeway.Api.Controllers;

[Route("auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Email and password are required" });
        }

        var user = await _authService.ValidateCredentialsAsync(request.Email, request.Password);
        if (user == null)
        {
            return Unauthorized(new { detail = "Invalid email or password" });
        }

        // Update last login
        await _authService.UpdateLastLoginAsync(user.Id);

        // Generate JWT
        var token = _authService.GenerateJwtToken(user);
        var expiryHours = int.Parse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["JWT_EXPIRY_HOURS"] ?? "24");

        var response = new LoginResponse
        {
            Token = token,
            User = MapToDto(user),
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
        };

        _logger.LogInformation("User {Email} logged in successfully", user.Email);
        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { detail = "Not authenticated" });
        }

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { detail = "User not found" });
        }

        return Ok(MapToDto(user));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { detail = "Not authenticated" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { detail = "New password must be at least 8 characters" });
        }

        var success = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (!success)
        {
            return BadRequest(new { detail = "Current password is incorrect" });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpPost("logout")]
    public ActionResult Logout()
    {
        // JWT tokens are stateless, so we just return success
        // The client should delete the token
        return Ok(new { message = "Logged out successfully" });
    }

    // ==================== Admin-only User Management ====================

    [HttpGet("users")]
    [RequireAdmin]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users.Select(MapToDto).ToList());
    }

    [HttpPost("users")]
    [RequireAdmin]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Email and password are required" });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { detail = "Password must be at least 8 characters" });
        }

        if (await _authService.EmailExistsAsync(request.Email))
        {
            return BadRequest(new { detail = "Email already exists" });
        }

        var user = await _authService.CreateUserAsync(request.Email, request.Password, request.Name, request.IsAdmin);

        _logger.LogInformation("Admin created new user: {Email}", user.Email);
        return StatusCode(201, MapToDto(user));
    }

    [HttpGet("users/{id:guid}")]
    [RequireAdmin]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { detail = "User not found" });
        }

        return Ok(MapToDto(user));
    }

    [HttpPatch("users/{id:guid}")]
    [RequireAdmin]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _authService.UpdateUserAsync(id, request.Name, request.Email, request.IsActive, request.IsAdmin);
        if (user == null)
        {
            return NotFound(new { detail = "User not found" });
        }

        _logger.LogInformation("Admin updated user: {Email}", user.Email);
        return Ok(MapToDto(user));
    }

    [HttpDelete("users/{id:guid}")]
    [RequireAdmin]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        // Prevent self-deletion
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim != null && Guid.TryParse(currentUserIdClaim.Value, out var currentUserId))
        {
            if (currentUserId == id)
            {
                return BadRequest(new { detail = "Cannot delete your own account" });
            }
        }

        var success = await _authService.DeleteUserAsync(id);
        if (!success)
        {
            return NotFound(new { detail = "User not found" });
        }

        _logger.LogInformation("Admin deleted user: {UserId}", id);
        return NoContent();
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Name = user.Name,
        IsAdmin = user.IsAdmin,
        CreatedAt = user.CreatedAt,
        IsActive = user.IsActive,
        LastLoginAt = user.LastLoginAt
    };
}
