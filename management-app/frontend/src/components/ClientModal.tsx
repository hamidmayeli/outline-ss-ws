import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';
import type { Client, CreateClientRequest, UpdateClientRequest } from '../services/api';
import { formatBytes } from '../utils/formatBytes';
import './ClientModal.css';

interface ClientModalProps {
  client: Client | null;
  onClose: (saved: boolean) => void;
}

export const ClientModal: React.FC<ClientModalProps> = ({ client, onClose }) => {
  const [name, setName] = useState(client?.name || '');
  const [limitInput, setLimitInput] = useState(client?.limit != null ? formatBytes(client.limit) : '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { token } = useAuth();

  const isEdit = !!client;

  const parseHumanBytes = (value: string): number | null | undefined => {
    const trimmed = value.trim();
    if (!trimmed) return null;

    const match = trimmed.match(/^([0-9]+(?:\.[0-9]+)?)\s*([a-zA-Z]{0,2})$/);
    if (!match) return undefined;

    const number = Number(match[1]);
    if (Number.isNaN(number) || number < 0) return undefined;

    const unit = match[2].toUpperCase();
    const multipliers: Record<string, number> = {
      '': 1,
      B: 1,
      K: 1024,
      KB: 1024,
      M: 1024 ** 2,
      MB: 1024 ** 2,
      G: 1024 ** 3,
      GB: 1024 ** 3,
      T: 1024 ** 4,
      TB: 1024 ** 4,
    };

    if (!(unit in multipliers)) return undefined;

    return Math.round(number * multipliers[unit]);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token) return;

    setError('');
    setLoading(true);

    const parsedLimit = parseHumanBytes(limitInput);
    if (parsedLimit === undefined) {
      setError('Invalid limit format. Use values like 500MB or 5GB.');
      setLoading(false);
      return;
    }

    try {
      if (isEdit) {
        const request: UpdateClientRequest = { name, limit: parsedLimit };
        await api.updateClient(client.id, request);
      } else {
        const request: CreateClientRequest = { name, limit: parsedLimit };
        await api.createClient(request);
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

          <div className="form-group">
            <label htmlFor="limit">Data Limit</label>
            <input
              type="text"
              id="limit"
              value={limitInput}
              onChange={(e) => setLimitInput(e.target.value)}
              disabled={loading}
              placeholder="e.g. 500MB or 5GB"
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
