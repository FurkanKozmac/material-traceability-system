export const getStoredUser = () => {
  try {
    const raw = localStorage.getItem('user');
    if (!raw) return null;
    const user = JSON.parse(raw);
    return user && typeof user.username === 'string' &&
      typeof (user.accessToken || user.token) === 'string' ? user : null;
  } catch {
    return null;
  }
};

export const currentUsername = (user = getStoredUser()) => user?.username || '';

export const clearSession = () => localStorage.removeItem('user');

export const isAdmin = (user = getStoredUser()) => Boolean(
  user?.roles?.some((role) => role === 'Admin' || role === 'ROLE_ADMIN'));

export const isOperator = (user = getStoredUser()) => Boolean(
  user?.roles?.some((role) => role === 'Operator' || role === 'ROLE_OPERATOR'));

export const hasAnyPermission = (required = [], user = getStoredUser()) => {
  if (!user) return false;
  if (isAdmin(user) || required.length === 0) return true;
  return required.some((permission) => user.permissions?.includes(permission));
};
