import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useTheme } from '../contexts/ThemeContext';
import './Layout.css';

interface LayoutProps {
  children: React.ReactNode;
}

export const Layout: React.FC<LayoutProps> = ({ children }) => {
  const { isAuthenticated, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const [showReportsDropdown, setShowReportsDropdown] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  if (!isAuthenticated) {
    return <>{children}</>;
  }

  return (
    <div className="layout">
      <header className="header">
        <div className="header-content">
          <div className="header-left">
            <nav className="nav">
              <Link 
                to="/clients" 
                className={`nav-link ${location.pathname === '/clients' ? 'active' : ''}`}
              >
                Clients
              </Link>
              <div className="nav-dropdown">
                <button 
                  className={`nav-link dropdown-toggle ${location.pathname.startsWith('/reports') ? 'active' : ''}`}
                  onClick={() => setShowReportsDropdown(!showReportsDropdown)}
                >
                  Reports ▾
                </button>
                {showReportsDropdown && (
                  <div className="dropdown-menu">
                    <Link 
                      to="/reports/piechart" 
                      className="dropdown-item"
                      onClick={() => setShowReportsDropdown(false)}
                    >
                      Pie Chart
                    </Link>
                  </div>
                )}
              </div>
            </nav>
          </div>
          <div className="header-right">
            <button 
              className="theme-toggle" 
              onClick={toggleTheme}
              title={`Switch to ${theme === 'light' ? 'dark' : 'light'} mode`}
            >
              {theme === 'light' ? '🌙' : '☀️'}
            </button>
            <button className="logout-btn" onClick={handleLogout}>
              Logout
            </button>
          </div>
        </div>
      </header>
      <main className="main-content">
        {children}
      </main>
    </div>
  );
};
