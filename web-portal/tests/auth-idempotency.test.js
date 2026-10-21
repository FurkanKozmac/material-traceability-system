import test from 'node:test';
import assert from 'node:assert/strict';

const values = new Map();
globalThis.localStorage = {
  getItem: (key) => values.get(key) ?? null,
  setItem: (key, value) => values.set(key, String(value)),
  removeItem: (key) => values.delete(key),
};
const {
  clearSession, currentUsername, getStoredUser, hasAnyPermission, isAdmin, isOperator,
} = await import('../src/auth.js');
const { canAccessAdmin } = await import('../src/domain/access.js');
const { pendingOperation } = await import('../src/idempotency.js');

test('stored user rejects malformed or incomplete session data', () => {
  values.set('user', '{bad-json');
  assert.equal(getStoredUser(), null);
  values.set('user', JSON.stringify({ roles: ['ROLE_ADMIN'] }));
  assert.equal(getStoredUser(), null);
});

test('admin bypasses permission checks and operators require an explicit permission', () => {
  const admin = { username: 'admin', roles: ['ROLE_ADMIN'], permissions: [] };
  const operator = { username: 'op', roles: ['Operator'], permissions: ['READ_STOCK'] };
  assert.equal(isAdmin(admin), true);
  assert.equal(isOperator(operator), true);
  assert.equal(isOperator(admin), false);
  assert.equal(isAdmin(operator), false);
  assert.equal(canAccessAdmin(admin), true);
  assert.equal(canAccessAdmin(operator), false);
  assert.equal(hasAnyPermission(['MANAGE_SECURITY'], admin), true);
  assert.equal(hasAnyPermission(['READ_STOCK'], operator), true);
  assert.equal(hasAnyPermission(['MANAGE_SECURITY'], operator), false);
});

test('current username is read from session and clearSession removes it', () => {
  values.set('user', JSON.stringify({
    username: 'operator.one', accessToken: 'token', roles: ['Operator'],
  }));
  assert.equal(currentUsername(), 'operator.one');
  clearSession();
  assert.equal(getStoredUser(), null);
});

test('pending operation key survives retry and is cleared only after success', () => {
  const first = pendingOperation('receive:42');
  const retry = pendingOperation('receive:42');
  assert.equal(retry.key, first.key);
  first.clear();
  assert.notEqual(pendingOperation('receive:42').key, first.key);
});
