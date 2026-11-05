using System.ComponentModel.DataAnnotations;

namespace Users.Contracts;

public record RegisterRequest(
    [Required]
    [StringLength(100, MinimumLength = 3)]
    string FullName,

    [Required]
    [EmailAddress]
    [StringLength(256)]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 8)]
    string Password
);

public record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(256)]
    string Email,

    [Required]
    string Password
);

public record RefreshTokenRequest(
    [Required]
    string RefreshToken
);

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role
);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    UserDto User
);