import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Login } from './pages/Login';
import { Clients } from './pages/Clients';
import { PieChart } from './pages/PieChart';
import { HourlyLineChart } from './pages/HourlyLineChart';
import { WeeklyComparisonChart } from './pages/WeeklyComparisonChart';
import UpdateNotification from './components/UpdateNotification';
import './App.css';

const App: React.FC = () => {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Layout>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route
                path="/clients"
                element={
                  <ProtectedRoute>
                    <Clients />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/reports/piechart"
                element={
                  <ProtectedRoute>
                    <PieChart />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/reports/hourly"
                element={
                  <ProtectedRoute>
                    <HourlyLineChart />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/reports/weekly"
                element={
                  <ProtectedRoute>
                    <WeeklyComparisonChart />
                  </ProtectedRoute>
                }
              />
              <Route path="/" element={<Navigate to="/clients" replace />} />
              <Route path="*" element={<Navigate to="/clients" replace />} />
            </Routes>
          </Layout>
          <UpdateNotification />
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
};

export default App;
