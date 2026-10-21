import React from 'react';
import { Navigate } from 'react-router-dom';
import { canAccessAdmin } from '../domain/access';

// The backend remains the authority; this guard only keeps the admin UI out of the way.
export default function AdminRoute({ children }) {
  return canAccessAdmin() ? children : <Navigate to="/dashboard" replace />;
}
