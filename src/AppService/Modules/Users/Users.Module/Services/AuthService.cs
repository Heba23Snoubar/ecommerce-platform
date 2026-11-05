using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Errors;
using Users.Contracts;
using Users.Module.Data;

namespace Users.Module.Services;

internal sealed class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime =
        TimeSpan.FromDays(7);

    private readonly UsersDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(
        UsersDbContext dbContext,
        PasswordService passwordService,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var emailExists = await _dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
            throw new AppException(409, "EMAIL_ALREADY_EXISTS",
                "An account with this email already exists.");

        var customerRole = await _dbContext.Roles
            .FirstOrDefaultAsync(
                role => role.Id ==
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                cancellationToken);

        if (customerRole is null)
            throw new InvalidOperationException(
                "Customer role is missing.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        user.PasswordHash = _passwordService.HashPassword(
            user, request.Password);

        user.UserRoles.Add(new UserRoleAssignment
        {
            User = user,
            Role = customerRole,
            AssignedAtUtc = DateTime.UtcNow
        });

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException
                  && postgresException.SqlState ==
                     PostgresErrorCodes.UniqueViolation)
        {
            throw new AppException(409, "EMAIL_ALREADY_EXISTS",
                "An account with this email already exists.");
        }

        return await CreateAuthResponseAsync(
            user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail =
            request.Email.Trim().ToUpperInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null ||
            !_passwordService.VerifyPassword(
                user, request.Password, user.PasswordHash))
        {
            throw new AppException(401, "INVALID_CREDENTIALS",
                "Invalid email or password.");
        }

        if (!user.IsActive)
            throw new AppException(403, "ACCOUNT_DISABLED",
                "This account is disabled.");

        return await CreateAuthResponseAsync(
            user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var oldTokenHash = HashToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(
                token => token.TokenHash == oldTokenHash,
                cancellationToken);

        if (storedToken is null ||
            storedToken.RevokedAtUtc.HasValue ||
            storedToken.ExpiresAtUtc <= now)
        {
            throw new AppException(401, "INVALID_REFRESH_TOKEN",
                "Refresh token is invalid or expired.");
        }

        var user = storedToken.User;

        if (!user.IsActive)
            throw new AppException(403, "ACCOUNT_DISABLED",
                "This account is disabled.");

        var newRefreshToken = GenerateRefreshToken();
        var newTokenHash = HashToken(newRefreshToken);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Revoke the old token only if it is still active.
        var affectedRows = await _dbContext.RefreshTokens
            .Where(token =>
                token.Id == storedToken.Id &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        token => token.RevokedAtUtc, now)
                    .SetProperty(
                        token => token.ReplacedByTokenHash,
                        newTokenHash),
                cancellationToken);

        if (affectedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw new AppException(401, "INVALID_REFRESH_TOKEN",
                "Refresh token has already been used or revoked.");
        }

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(RefreshTokenLifetime)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await BuildAuthResponseAsync(
            user, newRefreshToken, cancellationToken);
    }

    public async Task LogoutAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;

        // Logout is idempotent: an unknown or already revoked
        // token does not cause an error.
        await _dbContext.RefreshTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAtUtc, now),
                cancellationToken);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var refreshToken = GenerateRefreshToken();
        var now = DateTime.UtcNow;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(RefreshTokenLifetime)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(
            user, refreshToken, cancellationToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(
        User user,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var assignments = await _dbContext.UserRoleAssignments
            .Where(item => item.UserId == user.Id)
            .Include(item => item.Role)
                .ThenInclude(role => role.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        var roles = assignments
            .Select(item => item.Role.Name)
            .Distinct()
            .ToArray();

        var permissions = assignments
            .SelectMany(item => item.Role.RolePermissions)
            .Select(item => item.Permission.Name)
            .Distinct()
            .ToArray();

        var tokenResult = _jwtTokenService.GenerateToken(
            user, roles, permissions);

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Role);

        return new AuthResponse(
            tokenResult.AccessToken,
            tokenResult.ExpiresAtUtc,
            refreshToken,
            userDto);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes);
    }
}