import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { getStoredUser } from '../auth';

export default function ProtectedRoute() {
  const user = getStoredUser();

  if (!user) {
    localStorage.removeItem('user');
    return <Navigate to="/login" replace />;
  }

  // Otherwise, render the child routes inside Layout
  return <Outlet />;
}
