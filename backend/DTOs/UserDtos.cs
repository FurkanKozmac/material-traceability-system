using System.ComponentModel.DataAnnotations;
using backend.Common.Security;

namespace backend.DTOs;

public record UserResponse(
    long Id,
    string Username,
    bool Active,
    string Role,
    List<string> Roles
);

public record CreateUserRequest(
    [Required,
     StringLength(UsernamePolicy.MaximumLength, MinimumLength = UsernamePolicy.MinimumLength),
     RegularExpression(UsernamePolicy.AllowedCharactersPattern,
         ErrorMessage = UsernamePolicy.AllowedCharactersErrorMessage)]
    string Username,
    [Required, MinLength(PasswordPolicy.MinimumLength), MaxLength(PasswordPolicy.MaximumLength)]
    string Password,
    [Required] string Role
);

public record UpdateUserRoleRequest([Required] string Role);

public record UpdateUserActiveRequest(bool Active);

public record ResetUserPasswordRequest(
    [Required, MinLength(PasswordPolicy.MinimumLength), MaxLength(PasswordPolicy.MaximumLength)]
    string NewPassword);

public record ChangePasswordRequest(
    [Required] string Username,
    [Required] string CurrentPassword,
    [Required, MinLength(PasswordPolicy.MinimumLength), MaxLength(PasswordPolicy.MaximumLength)]
    string NewPassword
);
