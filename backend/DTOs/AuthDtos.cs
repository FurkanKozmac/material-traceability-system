using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password,
    string? AuthSource
);

public record UserDto(
    long Id,
    string Username,
    string Role,
    List<string> Roles
);

public record LoginResponse(
    string Token,
    string AccessToken,
    string RefreshToken,
    string Username,
    string Role,
    List<string> Roles,
    UserDto User
);

public record RefreshTokenRequest([Required] string RefreshToken);

public record LogoutRequest([Required] string RefreshToken);
