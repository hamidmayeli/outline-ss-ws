import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';
import type { Client, CreateClientRequest, UpdateClientRequest } from '../services/api';
import './ClientModal.css';

interface ClientModalProps {
  client: Client | null;
  onClose: (saved: boolean) => void;
}

export const ClientModal: React.FC<ClientModalProps> = ({ client, onClose }) => {
  const [name, setName] = useState(client?.name || '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { token } = useAuth();

  const isEdit = !!client;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token) return;

    setError('');
    setLoading(true);

    try {
      if (isEdit) {
        const request: UpdateClientRequest = { name };
        await api.updateClient(token, client.id, request);
      } else {
        const request: CreateClientRequest = { name };
        await api.createClient(token, request);
      }
      onClose(true);
    } catch {
      setError('Failed to save client. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={() => onClose(false)}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{isEdit ? 'Edit Client' : 'Add Client'}</h2>
          <button className="modal-close" onClick={() => onClose(false)}>
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form">
          {error && <div className="error-message">{error}</div>}

          <div className="form-group">
            <label htmlFor="name">Client Name</label>
            <input
              type="text"
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              disabled={loading}
              placeholder="Enter client name"
              autoFocus
            />
          </div>

          {isEdit && (
            <div className="info-box">
              <p><strong>ID:</strong> {client.id}</p>
              <p><strong>Secret:</strong> <code>{client.secret}</code></p>
              <p><strong>Cipher:</strong> <code>{client.cipher}</code></p>
              <p><strong>Status:</strong> {client.isActive ? 'Active' : 'Inactive'}</p>
            </div>
          )}

          <div className="modal-actions">
            <button
              type="button"
              className="btn-cancel"
              onClick={() => onClose(false)}
              disabled={loading}
            >
              Cancel
            </button>
            <button type="submit" className="btn-save" disabled={loading}>
              {loading ? 'Saving...' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
