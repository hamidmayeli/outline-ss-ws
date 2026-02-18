import React, { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useTheme } from '../contexts/ThemeContext';
import { useConfirmDialog } from '../contexts/ConfirmContext';
import { ConfirmModal } from './ConfirmModal';
import './Layout.css';

interface LayoutProps {
  children: React.ReactNode;
}

export const Layout: React.FC<LayoutProps> = ({ children }) => {
  const { isAuthenticated, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const { dialog, handleConfirm, handleCancel } = useConfirmDialog();
  const navigate = useNavigate();
  const location = useLocation();
  const [showReportsDropdown, setShowReportsDropdown] = useState(false);
  const [showBurgerMenu, setShowBurgerMenu] = useState(false);
  const hostPrefix = window.location.host.split('.')[0];

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
                      Pie Chart Usage
                    </Link>
                    <Link
                      to="/reports/hourly"
                      className="dropdown-item"
                      onClick={() => setShowReportsDropdown(false)}
                    >
                      Hourly Usage
                    </Link>
                    <Link
                      to="/reports/weekly"
                      className="dropdown-item"
                      onClick={() => setShowReportsDropdown(false)}
                    >
                      Weekly Comparison
                    </Link>
                  </div>
                )}
              </div>
            </nav>
          </div>
          <div className="header-right">
            <span className="host-prefix">{hostPrefix}</span>
            <div className="burger-menu-container">
              <button
                className="burger-menu-toggle"
                onClick={() => setShowBurgerMenu(!showBurgerMenu)}
                aria-label="Open account menu"
                title="Menu"
              >
                ☰
              </button>
              {showBurgerMenu && (
                <div className="burger-menu-dropdown">
                  <button
                    className="burger-menu-item"
                    onClick={() => {
                      toggleTheme();
                      setShowBurgerMenu(false);
                    }}
                  >
                    {theme === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
                  </button>
                  <button
                    className="burger-menu-item"
                    onClick={() => {
                      setShowBurgerMenu(false);
                      handleLogout();
                    }}
                  >
                    Logout
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>
      <main className="main-content">
        {children}
      </main>
      {dialog.isOpen && (
        <ConfirmModal
          title={dialog.options.title ?? 'Confirm'}
          message={dialog.options.message}
          confirmLabel={dialog.options.confirmLabel ?? 'Confirm'}
          cancelLabel={dialog.options.cancelLabel ?? 'Cancel'}
          onConfirm={handleConfirm}
          onCancel={handleCancel}
        />
      )}
    </div>
  );
};
