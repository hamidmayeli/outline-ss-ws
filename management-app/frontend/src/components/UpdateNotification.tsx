import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { useRegisterSW } from 'virtual:pwa-register/react';
import './UpdateNotification.css';

export default function UpdateNotification() {
  const location = useLocation();
  const {
    offlineReady: [offlineReady, setOfflineReady],
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW({
    immediate: true,
    onRegisterError(error) {
      console.log('SW registration error', error);
    },
  });

  // Check for updates when page becomes visible or gains focus
  useEffect(() => {
    const checkForUpdate = async () => {
      if ('serviceWorker' in navigator) {
        const registration = await navigator.serviceWorker.getRegistration();
        if (registration) {
          registration.update();
        }
      }
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        checkForUpdate();
      }
    };

    const handleFocus = () => {
      checkForUpdate();
    };

    // Check on location change
    checkForUpdate();

    // Listen for visibility and focus changes
    document.addEventListener('visibilitychange', handleVisibilityChange);
    window.addEventListener('focus', handleFocus);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      window.removeEventListener('focus', handleFocus);
    };
  }, [location]);

  const close = () => {
    setOfflineReady(false);
    setNeedRefresh(false);
  };

  const handleUpdate = async () => {
    await updateServiceWorker(true);
  };

  if (!needRefresh && !offlineReady) return null;

  return (
    <div className="update-notification">
      <div className="update-notification-content">
        {offlineReady && (
          <p className="update-notification-text">App ready to work offline</p>
        )}
        {needRefresh && (
          <>
            <p className="update-notification-title">New version available!</p>
            <p className="update-notification-text">Click reload to update to the latest version.</p>
          </>
        )}
        <div className="update-notification-actions">
          {needRefresh && (
            <button
              onClick={handleUpdate}
              className="update-btn-primary"
            >
              Reload
            </button>
          )}
          <button
            onClick={close}
            className="update-btn-secondary"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
