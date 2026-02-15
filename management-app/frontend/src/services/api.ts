const API_BASE_URL = import.meta.env.VITE_API_HOST || '/api';

// Store the auth token internally
let authToken: string | null = null;

// Initialize token from localStorage if available
if (typeof window !== 'undefined' && window.localStorage) {
  authToken = localStorage.getItem('token');
}

// Function to set the auth token
export function setAuthToken(token: string | null) {
  authToken = token;
}

// Function to get the current auth token
export function getAuthToken(): string | null {
  return authToken;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}

export interface ClientUsage {
  totalBytesTransferred: number;
  bytesUploaded: number;
  bytesDownloaded: number;
  tunnelTimeSeconds: number;
  totalConnections: number;
}

export interface HourlyDataPoint {
  timestamp: string;
  bytesTransferred: number;
  connections: number;
}

export interface HourlyUsageResponse {
  clientId: string;
  clientName: string;
  dataPoints: HourlyDataPoint[];
}

export interface Client {
  id: number;
  name: string;
  secret: string;
  cipher: string;
  limit?: number | null;
  isActive: boolean;
  usageLast30Days?: ClientUsage;
}

export interface CreateClientRequest {
  name: string;
  limit?: number | null;
}

export interface UpdateClientRequest {
  name: string;
  limit?: number | null;
}

class ApiError extends Error {
  public status: number;
  
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = 'ApiError';
  }
}

async function fetchWithAuth(url: string, options: RequestInit = {}) {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  if (authToken) {
    headers['Authorization'] = `Bearer ${authToken}`;
  }

  const response = await fetch(url, {
    ...options,
    headers: {
      ...headers,
      ...(options.headers as Record<string, string> || {}),
    },
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new ApiError(response.status, errorText || response.statusText);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

export const api = {
  async login(request: LoginRequest): Promise<LoginResponse> {
    return fetchWithAuth(`${API_BASE_URL}/v1/auth/login`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async getClients(): Promise<Client[]> {
    return fetchWithAuth(`${API_BASE_URL}/v1/clients`);
  },

  async createClient(request: CreateClientRequest): Promise<Client> {
    return fetchWithAuth(`${API_BASE_URL}/v1/clients`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async updateClient(id: number, request: UpdateClientRequest): Promise<Client> {
    return fetchWithAuth(`${API_BASE_URL}/v1/clients/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  },

  async deleteClient(id: number): Promise<void> {
    return fetchWithAuth(`${API_BASE_URL}/v1/clients/${id}`, {
      method: 'DELETE',
    });
  },

  async getHourlyUsage(hours = 24): Promise<HourlyUsageResponse[]> {
    return fetchWithAuth(`${API_BASE_URL}/v1/reports/hourly?hours=${hours}`);
  },
};

export { ApiError };
