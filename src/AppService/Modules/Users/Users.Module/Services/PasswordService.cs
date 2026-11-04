
using Microsoft.AspNetCore.Identity;
using Users.Module.Data;

namespace Users.Module.Services;

internal sealed class PasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(
        User user,
        string password,
        string passwordHash)
    {
        var result = _passwordHasher.VerifyHashedPassword(
            user,
            passwordHash,
            password
        );

        return result != PasswordVerificationResult.Failed;
    }
}
