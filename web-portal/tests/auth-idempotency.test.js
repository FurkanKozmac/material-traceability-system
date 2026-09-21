import test from 'node:test';
import assert from 'node:assert/strict';

const values = new Map();
globalThis.localStorage = {
  getItem: (key) => values.get(key) ?? null,
  setItem: (key, value) => values.set(key, String(value)),
  removeItem: (key) => values.delete(key),
};
const { getStoredUser, hasAnyPermission, isAdmin } = await import('../src/auth.js');
const { pendingOperation } = await import('../src/idempotency.js');

test('stored user rejects malformed or incomplete session data', () => {
  values.set('user', '{bad-json');
  assert.equal(getStoredUser(), null);
  values.set('user', JSON.stringify({ roles: ['ROLE_ADMIN'] }));
  assert.equal(getStoredUser(), null);
});

test('admin bypasses permission checks and operators require an explicit permission', () => {
  const admin = { username: 'admin', roles: ['ROLE_ADMIN'], permissions: [] };
  const operator = { username: 'op', roles: [], permissions: ['READ_STOCK'] };
  assert.equal(isAdmin(admin), true);
  assert.equal(hasAnyPermission(['MANAGE_SECURITY'], admin), true);
  assert.equal(hasAnyPermission(['READ_STOCK'], operator), true);
  assert.equal(hasAnyPermission(['MANAGE_SECURITY'], operator), false);
});

test('pending operation key survives retry and is cleared only after success', () => {
  const first = pendingOperation('receive:42');
  const retry = pendingOperation('receive:42');
  assert.equal(retry.key, first.key);
  first.clear();
  assert.notEqual(pendingOperation('receive:42').key, first.key);
});
