
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Users.Module.Data;

namespace Users.Module.Services;

internal sealed class JwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _secretKey = configuration["JWT_KEY"]
            ?? throw new InvalidOperationException(
                "JWT_KEY configuration is missing.");

        _issuer = configuration["JWT_ISSUER"]
            ?? throw new InvalidOperationException(
                "JWT_ISSUER configuration is missing.");

        _audience = configuration["JWT_AUDIENCE"]
            ?? throw new InvalidOperationException(
                "JWT_AUDIENCE configuration is missing.");

        if (Encoding.UTF8.GetByteCount(_secretKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT_KEY must contain at least 32 bytes.");
        }

        _expirationMinutes = int.TryParse(
            configuration["JWT_EXPIRES_MINUTES"],
            out var minutes)
            ? minutes
            : 60;

        if (_expirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT_EXPIRES_MINUTES must be greater than zero.");
        }
    }

    public (string AccessToken, DateTime ExpiresAtUtc) GenerateToken(
    User user,
    IReadOnlyCollection<string> roles,
    IReadOnlyCollection<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_expirationMinutes);

        var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new(ClaimTypes.Name, user.FullName),
    new(ClaimTypes.Email, user.Email),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};

        foreach (var role in roles.Distinct())
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions.Distinct())
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_secretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return (accessToken, expiresAt);
    }
}
