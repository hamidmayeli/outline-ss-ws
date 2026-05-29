import React, { useState, useRef } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import type { Client } from '../services/api';
import './ClientConfigModal.css';

interface ClientConfigModalProps {
  client: Client;
  onClose: () => void;
}

export const ClientConfigModal: React.FC<ClientConfigModalProps> = ({ client, onClose }) => {
  const [copied, setCopied] = useState(false);
  const [qrCopied, setQrCopied] = useState(false);
  const qrRef = useRef<HTMLDivElement>(null);

  const configUrl = `ssconf://${window.location.host}/api/v1/config/${client.id}#${window.location.host.split('.')[0]}-${client.name}`;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(configUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      alert('Failed to copy to clipboard');
    }
  };

  const handleCopyQR = async () => {
    try {
      if (!qrRef.current) return;
      const canvas = document.createElement('canvas');
      const svg = qrRef.current.querySelector('svg');
      if (!svg) return;
      
      const svgData = new XMLSerializer().serializeToString(svg);
      const img = new Image();
      img.onload = () => {
        canvas.width = img.width;
        canvas.height = img.height;
        const ctx = canvas.getContext('2d');
        if (ctx) {
          ctx.fillStyle = 'white';
          ctx.fillRect(0, 0, canvas.width, canvas.height);
          ctx.drawImage(img, 0, 0);
        }
        canvas.toBlob((blob) => {
          if (blob) {
            navigator.clipboard.write([
              new ClipboardItem({ 'image/png': blob })
            ]);
            setQrCopied(true);
            setTimeout(() => setQrCopied(false), 2000);
          }
        });
      };
      img.src = 'data:image/svg+xml;base64,' + btoa(svgData);
    } catch {
      alert('Failed to copy QR code to clipboard');
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

          <div className="config-section">
            <h3>QR Code</h3>
            <p className="config-description">
              Scan this QR code to automatically configure the Outline app:
            </p>
            <div className="qr-code-box">
              <div ref={qrRef}>
                <QRCodeSVG 
                  value={configUrl}
                  size={200}
                  level="H"
                  includeMargin={true}
                />
              </div>
              <button 
                className={`btn-copy-qr ${qrCopied ? 'copied' : ''}`}
                onClick={handleCopyQR}
              >
                {qrCopied ? '✓ Copied' : '📋 Copy QR'}
              </button>
            </div>
          </div>

          <div className="config-note">
            <strong>Note:</strong> Keep this information secure. Anyone with the configuration URL or QR code can access the VPN connection.
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
