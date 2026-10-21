import test from 'node:test';
import assert from 'node:assert/strict';
import { validatePasswordChange } from '../src/domain/passwordValidation.js';

test('password change validation enforces policy and confirmation rules', () => {
  assert.equal(validatePasswordChange({
    currentPassword: 'current-password',
    newPassword: 'different-password-123',
    confirmPassword: 'different-password-123',
  }), null);

  assert.equal(validatePasswordChange({
    currentPassword: 'current-password',
    newPassword: 'short',
    confirmPassword: 'short',
  }), 'passwordTooShort');

  assert.equal(validatePasswordChange({
    currentPassword: 'current-password',
    newPassword: 'x'.repeat(129),
    confirmPassword: 'x'.repeat(129),
  }), 'passwordTooLong');

  assert.equal(validatePasswordChange({
    currentPassword: 'current-password',
    newPassword: 'different-password-123',
    confirmPassword: 'different-password-124',
  }), 'passwordMismatch');

  assert.equal(validatePasswordChange({
    currentPassword: 'current-password',
    newPassword: 'current-password',
    confirmPassword: 'current-password',
  }), 'passwordUnchanged');
});
