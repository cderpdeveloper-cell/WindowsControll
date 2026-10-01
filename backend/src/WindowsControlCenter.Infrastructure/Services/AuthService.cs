using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Entities;
using WindowsControlCenter.Domain.Enums;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;

    public AuthService(AppDbContext db, IConfiguration config, IAuditService audit)
    {
        _db = db;
        _config = config;
        _audit = audit;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Email already registered.");

        // Create organization if provided, or use default
        var orgName = request.OrganizationName ?? "Default Organization";
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Name == orgName);
        if (org == null)
        {
            org = new Organization
            {
                Name = orgName,
                Slug = orgName.ToLowerInvariant().Replace(" ", "-").Replace(".", "-"),
                Description = $"Organization created during registration"
            };
            _db.Organizations.Add(org);
            await _db.SaveChangesAsync();
        }

        // Determine role — first user in org is SuperAdmin
        var isFirstUser = !await _db.Users.AnyAsync(u => u.OrganizationId == org.Id);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = isFirstUser ? UserRole.SuperAdmin : UserRole.Viewer,
            IsActive = true,
            OrganizationId = org.Id,
            LastLoginAt = DateTime.UtcNow,
            LastLoginIp = ipAddress
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken(user.Id, ipAddress);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("User.Register", "Auth", $"User {user.Username} registered",
            userId: user.Id, username: user.Username, ipAddress: ipAddress,
            result: "Success", organizationId: org.Id);

        return new AuthResponse(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            MapToUserDto(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await _db.Users.Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            if (user != null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();

                await _audit.LogAsync("User.LoginFailed", "Auth", $"Failed login for {request.Email}",
                    userId: user.Id, username: user.Username, ipAddress: ipAddress,
                    result: "Failed", organizationId: user.OrganizationId);
            }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Account locked. Try again later.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled.");

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken(user.Id, ipAddress);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("User.Login", "Auth", $"User {user.Username} logged in",
            userId: user.Id, username: user.Username, ipAddress: ipAddress,
            result: "Success", organizationId: user.OrganizationId);

        return new AuthResponse(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            MapToUserDto(user));
    }

    public async Task<AuthResponse> RefreshTokenAsync(string token, string ipAddress)
    {
        var refreshToken = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken == null || !refreshToken.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate token
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = ipAddress;

        var newRefreshToken = GenerateRefreshToken(refreshToken.UserId, ipAddress);
        refreshToken.ReplacedByToken = newRefreshToken.Token;
        _db.RefreshTokens.Add(newRefreshToken);

        var user = refreshToken.User;
        var accessToken = GenerateAccessToken(user);
        await _db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            newRefreshToken.Token,
            DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            MapToUserDto(user));
    }

    public async Task RevokeTokenAsync(string token, string ipAddress)
    {
        var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
        if (refreshToken == null || !refreshToken.IsActive)
            throw new InvalidOperationException("Token not found or already revoked.");

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = ipAddress;
        await _db.SaveChangesAsync();
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user == null ? null : MapToUserDto(user);
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("organizationId", user.OrganizationId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetTokenExpirationMinutes()),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private RefreshToken GenerateRefreshToken(Guid userId, string ipAddress)
    {
        return new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress,
            UserId = userId
        };
    }

    private int GetTokenExpirationMinutes() => int.TryParse(_config["Jwt:ExpirationMinutes"], out var min) ? min : 15;

    private static UserDto MapToUserDto(User user) => new(
        user.Id, user.Username, user.Email, user.FirstName, user.LastName,
        user.Role, user.IsActive, user.LastLoginAt, user.OrganizationId);
}
