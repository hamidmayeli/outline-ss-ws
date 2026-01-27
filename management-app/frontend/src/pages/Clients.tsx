import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api, ApiError } from '../services/api';
import type { Client } from '../services/api';
import { ClientModal } from '../components/ClientModal';
import { ClientConfigModal } from '../components/ClientConfigModal';
import './Clients.css';

const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
};

export const Clients: React.FC = () => {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showConfigModal, setShowConfigModal] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [selectedClient, setSelectedClient] = useState<Client | null>(null);
  const { token, logout } = useAuth();

  const loadClients = useCallback(async () => {
    if (!token) return;
    
    try {
      setError('');
      const data = await api.getClients(token);
      setClients(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout();
      } else {
        setError('Failed to load clients');
      }
    } finally {
      setLoading(false);
    }
  }, [token, logout]);

  useEffect(() => {
    loadClients();
  }, [loadClients]);

  const handleCreate = () => {
    setEditingClient(null);
    setShowModal(true);
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadClients();
    setRefreshing(false);
  };

  const handleEdit = (client: Client) => {
    setEditingClient(client);
    setShowModal(true);
  };

  const handleDelete = async (id: number) => {
    if (!token) return;
    if (!confirm('Are you sure you want to delete this client?')) return;

    try {
      await api.deleteClient(token, id);
      await loadClients();
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        logout();
      } else {
        alert('Failed to delete client');
      }
    }
  };

  const handleShowConfig = (client: Client) => {
    setSelectedClient(client);
    setShowConfigModal(true);
  };

  const handleModalClose = async (saved: boolean) => {
    setShowModal(false);
    setEditingClient(null);
    if (saved) {
      await loadClients();
    }
  };

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Loading clients...</p>
      </div>
    );
  }

  return (
    <div className="clients-container">
      <div className="clients-header">
        <h1>Clients</h1>
        <div>
          <button 
            className="btn-secondary" 
            onClick={handleRefresh}
            disabled={refreshing}
            style={{ marginRight: '0.5rem' }}
          >
            {refreshing ? '↻ Refreshing...' : '↻ Refresh'}
          </button>
          <button className="btn-primary" onClick={handleCreate}>
            + Add Client
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {clients.length === 0 ? (
        <div className="empty-state">
          <p>No clients yet</p>
          <button className="btn-secondary" onClick={handleCreate}>
            Create your first client
          </button>
        </div>
      ) : (
        <div className="clients-table-wrapper">
          <table className="clients-table">
            <thead>
              <tr>
                <th>Name</th>
                <th className="status-column">Status</th>
                <th>Data Usage</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {clients.map((client) => (
                <tr key={client.id}>
                  <td>{client.name}</td>
                  <td className="status-column">
                    <span className={`status-badge ${client.isActive ? 'active' : 'inactive'}`}>
                      {client.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    {client.usageLast30Days ? (
                      <>
                        <div className="usage-stat">
                          ↑ {formatBytes(client.usageLast30Days.bytesUploaded)}
                        </div>
                        <div className="usage-stat">
                          ↓ {formatBytes(client.usageLast30Days.bytesDownloaded)}
                        </div>
                      </>
                    ) : (
                      <span className="text-muted">No data</span>
                    )}
                  </td>
                  <td className="actions-cell">
                    <button
                      className="btn-action btn-config"
                      onClick={() => handleShowConfig(client)}
                      title="View Config"
                    >
                      📋
                    </button>
                    <button
                      className="btn-action btn-edit"
                      onClick={() => handleEdit(client)}
                      title="Edit"
                    >
                      ✏️
                    </button>
                    <button
                      className="btn-action btn-delete"
                      onClick={() => handleDelete(client.id)}
                      title="Delete"
                    >
                      🗑️
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showModal && (
        <ClientModal
          client={editingClient}
          onClose={handleModalClose}
        />
      )}

      {showConfigModal && selectedClient && (
        <ClientConfigModal
          client={selectedClient}
          onClose={() => setShowConfigModal(false)}
        />
      )}
    </div>
  );
};
