import React, { useState } from 'react';
import type { Client } from '../services/api';
import './ClientConfigModal.css';

interface ClientConfigModalProps {
  client: Client;
  onClose: () => void;
}

export const ClientConfigModal: React.FC<ClientConfigModalProps> = ({ client, onClose }) => {
  const [copied, setCopied] = useState(false);

  let configUrl = `${window.location.origin}/api/v1/config/${client.id}`;

  // replace http/https with ssconf scheme
  configUrl = configUrl.replace(/^https?:\/\//, 'ssconf://');

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(configUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      alert('Failed to copy to clipboard');
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Client Configuration</h2>
          <button className="modal-close" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="config-content">
          <div className="config-section">
            <h3>Configuration URL for {client.name}</h3>
            <p className="config-description">
              Share this URL with the client to automatically configure their Outline app:
            </p>
            <div className="config-url-box">
              <code>{configUrl}</code>
              <button 
                className={`btn-copy ${copied ? 'copied' : ''}`}
                onClick={handleCopy}
              >
                {copied ? '✓ Copied' : '📋 Copy'}
              </button>
            </div>
          </div>

          <div className="config-note">
            <strong>Note:</strong> Keep this information secure. Anyone with the configuration URL can access the VPN connection.
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn-close" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
