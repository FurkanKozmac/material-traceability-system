import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from './passwordValidation.js';

export const USERNAME_MIN_LENGTH = 3;
export const USERNAME_MAX_LENGTH = 100;
export const USERNAME_PATTERN = /^[A-Za-z0-9._-]+$/;
export const USER_ROLES = ['Admin', 'Operator'];

export function validateCreateUser({ username, password, role }) {
  if (!username) return 'usernameRequired';
  if (username.length < USERNAME_MIN_LENGTH) return 'usernameTooShort';
  if (username.length > USERNAME_MAX_LENGTH) return 'usernameTooLong';
  if (!USERNAME_PATTERN.test(username)) return 'usernameInvalidCharacters';
  if (!password) return 'newPasswordRequired';
  if (password.length < PASSWORD_MIN_LENGTH) return 'passwordTooShort';
  if (password.length > PASSWORD_MAX_LENGTH) return 'passwordTooLong';
  if (!USER_ROLES.includes(role)) return 'invalidRole';
  return null;
}

export function validateResetPassword(password) {
  if (!password) return 'newPasswordRequired';
  if (password.length < PASSWORD_MIN_LENGTH) return 'passwordTooShort';
  if (password.length > PASSWORD_MAX_LENGTH) return 'passwordTooLong';
  return null;
}
