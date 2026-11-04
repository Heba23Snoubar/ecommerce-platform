
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Errors;
using Users.Contracts;
using Users.Module.Data;

namespace Users.Module.Services;

internal sealed class AuthService : IAuthService
{
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

        var emailExists = await _dbContext.Users
            .AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (emailExists)
        {
            throw new AppException(
                409,
                "EMAIL_ALREADY_EXISTS",
                "An account with this email already exists.");
        }

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
            user,
            request.Password);

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException
                  && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                  && postgresException.ConstraintName == "IX_users_NormalizedEmail")
        {
            throw new AppException(
                409,
                "EMAIL_ALREADY_EXISTS",
                "An account with this email already exists.");
        }

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null ||
            !_passwordService.VerifyPassword(
                user,
                request.Password,
                user.PasswordHash))
        {
            throw new AppException(
                401,
                "INVALID_CREDENTIALS",
                "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new AppException(
                403,
                "ACCOUNT_DISABLED",
                "This account is disabled.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var tokenResult = _jwtTokenService.GenerateToken(user);

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Role);

        return new AuthResponse(
            tokenResult.AccessToken,
            tokenResult.ExpiresAtUtc,
            userDto);
    }
}
