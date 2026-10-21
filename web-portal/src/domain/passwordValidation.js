export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;

export function validatePasswordChange({ currentPassword, newPassword, confirmPassword }) {
  if (!currentPassword) return 'currentPasswordRequired';
  if (!newPassword) return 'newPasswordRequired';
  if (newPassword.length < PASSWORD_MIN_LENGTH) return 'passwordTooShort';
  if (newPassword.length > PASSWORD_MAX_LENGTH) return 'passwordTooLong';
  if (newPassword !== confirmPassword) return 'passwordMismatch';
  if (newPassword === currentPassword) return 'passwordUnchanged';
  return null;
}
