import React, { Suspense, lazy } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { LinearProgress } from '@mui/material';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import ErrorBoundary from './components/ErrorBoundary';
import { getStoredUser } from './auth';
import { LanguageProvider } from './LanguageContext';

const Landing = lazy(() => import('./pages/Landing'));
const Login = lazy(() => import('./pages/Login'));
const Dashboard = lazy(() => import('./pages/Dashboard'));
const ChemicalManagement = lazy(() => import('./pages/ChemicalManagement'));
const BatchManagement = lazy(() => import('./pages/BatchManagement'));
const AddressManagement = lazy(() => import('./pages/AddressManagement'));
const InventoryMaster = lazy(() => import('./pages/InventoryMaster'));
const QrLabelManager = lazy(() => import('./pages/QrLabelManager'));
const RelocationTasks = lazy(() => import('./pages/RelocationTasks'));
const SafetyAssistant = lazy(() => import('./pages/SafetyAssistant'));

// Smart redirect: token varsa dashboard'a, yoksa login'e
function SmartRedirect() {
  return getStoredUser() ? <Navigate to="/dashboard" replace /> : <Navigate to="/login" replace />;
}

function App() {
  return (
    <LanguageProvider>
      <Router>
        <ErrorBoundary>
          <Suspense fallback={<LinearProgress />}>
            <Routes>
              {/* Root: smart redirect based on auth */}
              <Route path="/" element={<SmartRedirect />} />
              {/* Public Routes */}
              <Route path="/guest" element={<Landing />} />
              <Route path="/login" element={<Login />} />
              
              {/* Protected Routes Wrapper */}
              <Route element={<ProtectedRoute />}>
                <Route element={<Layout />}>
                  <Route path="/dashboard" element={<Dashboard />} />
                  <Route path="/chemicals" element={<ChemicalManagement />} />
                  <Route path="/batches" element={<BatchManagement />} />
                  <Route path="/addresses" element={<AddressManagement />} />
                  <Route path="/units" element={<InventoryMaster />} />
                  <Route path="/qr-labels" element={<QrLabelManager />} />
                  <Route path="/relocation-tasks" element={<RelocationTasks />} />
                  <Route path="/safety-assistant" element={<SafetyAssistant />} />
                  <Route path="*" element={<Navigate to="/dashboard" replace />} />
                </Route>
              </Route>
            </Routes>
          </Suspense>
        </ErrorBoundary>
      </Router>
    </LanguageProvider>
  );
}

export default App;
