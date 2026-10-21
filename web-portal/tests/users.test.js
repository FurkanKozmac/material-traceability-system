import test from 'node:test';
import assert from 'node:assert/strict';
import { isSameUser, requiresRoleConfirmation } from '../src/domain/users.js';

test('self row detection is case-insensitive and works for admin and operator sessions', () => {
  const adminSession = { username: 'ADMIN.ONE', roles: ['Admin'] };
  const operatorSession = { username: 'operator.one', roles: ['Operator'] };

  assert.equal(isSameUser({ username: 'admin.one' }, adminSession), true);
  assert.equal(isSameUser({ username: 'admin.one' }, operatorSession), false);
  assert.equal(isSameUser({ username: ' operator.one ' }, operatorSession), true);
  assert.equal(isSameUser({ username: 'operator.one' }, adminSession), false);
  assert.equal(isSameUser({ username: '' }, adminSession), false);
});

test('role confirmation is required for self changes and admin demotion', () => {
  const adminSession = { username: 'admin.one', roles: ['Admin'] };
  assert.equal(requiresRoleConfirmation(
    { username: 'admin.one', role: 'Admin' }, 'Operator', adminSession), true);
  assert.equal(requiresRoleConfirmation(
    { username: 'other.admin', role: 'Admin' }, 'Operator', adminSession), true);
  assert.equal(requiresRoleConfirmation(
    { username: 'operator.one', role: 'Operator' }, 'Admin', adminSession), false);
});
