import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';

export default function UpdateNotification() {
  const location = useLocation();
  const [offlineReady, setOfflineReady] = useState(false);
  const [needRefresh, setNeedRefresh] = useState(false);
  const [registration, setRegistration] = useState<ServiceWorkerRegistration | null>(null);

  useEffect(() => {
    if (!('serviceWorker' in navigator) || !import.meta.env.PROD) return;

    let isMounted = true;

    const registerServiceWorker = async () => {
      try {
        const reg = await navigator.serviceWorker.register('/sw.js');
        if (!isMounted) return;
        setRegistration(reg);

        reg.addEventListener('updatefound', () => {
          const installing = reg.installing;
          if (!installing) return;

          installing.addEventListener('statechange', () => {
            if (installing.state === 'installed') {
              if (navigator.serviceWorker.controller) {
                setNeedRefresh(true);
              } else {
                setOfflineReady(true);
              }
            }
          });
        });
      } catch (error) {
        console.log('SW registration error', error);
      }
    };

    registerServiceWorker();

    return () => {
      isMounted = false;
    };
  }, []);

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
    if (!registration) return;

    const waiting = registration.waiting;
    if (waiting) {
      waiting.postMessage({ type: 'SKIP_WAITING' });
      const onControllerChange = () => {
        navigator.serviceWorker.removeEventListener('controllerchange', onControllerChange);
        window.location.reload();
      };
      navigator.serviceWorker.addEventListener('controllerchange', onControllerChange);
      return;
    }

    await registration.update();
  };

  if (!needRefresh && !offlineReady) return null;

  return (
    <div className="fixed bottom-4 right-4 bg-blue-600 text-white p-4 rounded-lg shadow-lg z-50 max-w-sm">
      <div className="flex flex-col gap-2">
        {offlineReady && (
          <p className="text-sm">App ready to work offline</p>
        )}
        {needRefresh && (
          <>
            <p className="text-sm font-semibold">New version available!</p>
            <p className="text-xs">Click reload to update to the latest version.</p>
          </>
        )}
        <div className="flex gap-2 mt-2">
          {needRefresh && (
            <button
              onClick={handleUpdate}
              className="bg-white text-blue-600 px-4 py-2 rounded text-sm font-semibold hover:bg-gray-100"
            >
              Reload
            </button>
          )}
          <button
            onClick={close}
            className="bg-blue-500 text-white px-4 py-2 rounded text-sm hover:bg-blue-400"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
