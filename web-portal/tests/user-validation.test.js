import test from 'node:test';
import assert from 'node:assert/strict';
import { validateCreateUser, validateResetPassword } from '../src/domain/userValidation.js';

test('create user validation applies username, password, and role rules', () => {
  assert.equal(validateCreateUser({
    username: 'new.user-01', password: 'valid-password-123', role: 'Operator',
  }), null);

  assert.equal(validateCreateUser({
    username: 'ab', password: 'valid-password-123', role: 'Operator',
  }), 'usernameTooShort');

  assert.equal(validateCreateUser({
    username: 'invalid user', password: 'valid-password-123', role: 'Operator',
  }), 'usernameInvalidCharacters');

  assert.equal(validateCreateUser({
    username: 'valid-user', password: 'short', role: 'Operator',
  }), 'passwordTooShort');

  assert.equal(validateCreateUser({
    username: 'valid-user', password: 'valid-password-123', role: 'Viewer',
  }), 'invalidRole');
});

test('reset password validation applies the 12-128 character policy', () => {
  assert.equal(validateResetPassword('valid-password-123'), null);
  assert.equal(validateResetPassword('short'), 'passwordTooShort');
  assert.equal(validateResetPassword('x'.repeat(129)), 'passwordTooLong');
});
