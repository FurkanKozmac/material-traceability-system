import { currentUsername } from '../auth.js';

const normalizeUsername = (value) => (typeof value === 'string' ? value.trim().toLowerCase() : '');

export function isSameUser(rowUser, storedUser = null) {
  const rowUsername = normalizeUsername(rowUser?.username);
  if (!rowUsername) return false;

  const storedUsername = normalizeUsername(
    storedUser?.username ?? storedUser?.user?.username ?? currentUsername(),
  );
  return rowUsername === storedUsername;
}

export function requiresRoleConfirmation(rowUser, nextRole, storedUser = null) {
  const removesAdmin = rowUser?.role === 'Admin' && nextRole !== 'Admin';
  return isSameUser(rowUser, storedUser) || removesAdmin;
}
