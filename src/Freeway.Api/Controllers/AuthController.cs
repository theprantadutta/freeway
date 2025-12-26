using System.Security.Claims;
using Freeway.Application.DTOs;
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
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Username and password are required" });
        }

        var user = await _authService.ValidateCredentialsAsync(request.Username, request.Password);
        if (user == null)
        {
            return Unauthorized(new { detail = "Invalid username or password" });
        }

        // Update last login
        await _authService.UpdateLastLoginAsync(user.Id);

        // Generate JWT
        var token = _authService.GenerateJwtToken(user);
        var expiryHours = int.Parse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["JWT_EXPIRY_HOURS"] ?? "24");

        var response = new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt
            },
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
        };

        _logger.LogInformation("User {Username} logged in successfully", user.Username);
        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { detail = "Username and password are required" });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { detail = "Password must be at least 8 characters" });
        }

        // Check if this is the first user (allow registration) or if registration is restricted
        var userCount = await _authService.GetUserCountAsync();
        if (userCount > 0)
        {
            // Only allow registration if the request comes from an authenticated admin
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { detail = "Registration is restricted. Please contact an administrator." });
            }
        }

        // Check if username already exists
        if (await _authService.UsernameExistsAsync(request.Username))
        {
            return BadRequest(new { detail = "Username already exists" });
        }

        // Create user
        var user = await _authService.CreateUserAsync(request.Username, request.Password, request.Email);

        // Generate JWT
        var token = _authService.GenerateJwtToken(user);
        var expiryHours = int.Parse(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["JWT_EXPIRY_HOURS"] ?? "24");

        var response = new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt
            },
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
        };

        _logger.LogInformation("New user registered: {Username}", user.Username);
        return StatusCode(201, response);
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

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        });
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
}
