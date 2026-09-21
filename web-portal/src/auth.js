export const getStoredUser = () => {
  try {
    const raw = localStorage.getItem('user');
    if (!raw) return null;
    const user = JSON.parse(raw);
    return user && typeof user.username === 'string' ? user : null;
  } catch {
    return null;
  }
};

export const isAdmin = (user = getStoredUser()) => Boolean(
  user?.roles?.some((role) => role === 'Admin' || role === 'ROLE_ADMIN'));

export const hasAnyPermission = (required = [], user = getStoredUser()) => {
  if (!user) return false;
  if (isAdmin(user) || required.length === 0) return true;
  return required.some((permission) => user.permissions?.includes(permission));
};
