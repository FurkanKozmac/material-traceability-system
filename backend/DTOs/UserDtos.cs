using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record UserResponse(
    long Id,
    string Username,
    bool Active,
    string Role,
    List<string> Roles
);

public record CreateUserRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Username,
    [Required, MinLength(12)] string Password,
    [Required] string Role
);

public record UpdateUserRoleRequest([Required] string Role);

public record UpdateUserActiveRequest(bool Active);

public record ResetUserPasswordRequest([Required, MinLength(12)] string NewPassword);

public record ChangePasswordRequest(
    [Required] string Username,
    [Required] string CurrentPassword,
    [Required, MinLength(12)] string NewPassword
);
