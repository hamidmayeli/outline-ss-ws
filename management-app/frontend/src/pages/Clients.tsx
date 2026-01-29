import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api, ApiError } from '../services/api';
import type { Client } from '../services/api';
import { ClientModal } from '../components/ClientModal';
import { ClientConfigModal } from '../components/ClientConfigModal';
import { formatBytes } from '../utils/formatBytes';
import './Clients.css';

type SortColumn = 'name' | 'usage';
type SortDirection = 'asc' | 'desc';

export const Clients: React.FC = () => {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showConfigModal, setShowConfigModal] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [selectedClient, setSelectedClient] = useState<Client | null>(null);
  const [sortColumn, setSortColumn] = useState<SortColumn | null>(null);
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
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

  const handleSort = (column: SortColumn) => {
    if (sortColumn === column) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(column);
      setSortDirection('asc');
    }
  };

  const getTotalUsage = (client: Client): number => {
    if (!client.usageLast30Days) return 0;
    return client.usageLast30Days.bytesUploaded + client.usageLast30Days.bytesDownloaded;
  };

  const sortedClients = [...clients].sort((a, b) => {
    if (!sortColumn) return 0;

    let comparison = 0;
    if (sortColumn === 'name') {
      comparison = a.name.localeCompare(b.name);
    } else if (sortColumn === 'usage') {
      comparison = getTotalUsage(a) - getTotalUsage(b);
    }

    return sortDirection === 'asc' ? comparison : -comparison;
  });

  const getTotalUsageForAllClients = () => {
    const totalUploaded = clients.reduce((sum, client) => 
      sum + (client.usageLast30Days?.bytesUploaded || 0), 0);
    const totalDownloaded = clients.reduce((sum, client) => 
      sum + (client.usageLast30Days?.bytesDownloaded || 0), 0);
    const total = totalUploaded + totalDownloaded;
    
    return `↑ ${formatBytes(totalUploaded)}, ↓ ${formatBytes(totalDownloaded)}, (${formatBytes(total)})`;
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
        <div>
          <h1>Clients</h1>
          {clients.length > 0 && (
            <p className="total-usage-subheader">
              Total usage (last 30 days): {getTotalUsageForAllClients()}
            </p>
          )}
        </div>
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
                <th className="sortable" onClick={() => handleSort('name')}>
                  Name {sortColumn === 'name' && (sortDirection === 'asc' ? '↑' : '↓')}
                </th>
                <th className="status-column">Status</th>
                <th className="sortable" onClick={() => handleSort('usage')}>
                  Data Usage {sortColumn === 'usage' && (sortDirection === 'asc' ? '↑' : '↓')}
                </th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {sortedClients.map((client) => (
                <tr key={client.id}>
                  <td>
                    <span className={client.isActive ? '' : 'client-name-inactive'}>
                      {client.name}
                    </span>
                  </td>
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
                        <div className="usage-stat">
                          Limit: {client.limit != null ? formatBytes(client.limit) : 'No limit'}
                        </div>
                      </>
                    ) : (
                      <>
                        <span className="text-muted">No data</span>
                        <div className="usage-stat">
                          Limit: {client.limit != null ? formatBytes(client.limit) : 'No limit'}
                        </div>
                      </>
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
