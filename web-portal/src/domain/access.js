import { isAdmin } from '../auth.js';

export const canAccessAdmin = (user) => isAdmin(user);
