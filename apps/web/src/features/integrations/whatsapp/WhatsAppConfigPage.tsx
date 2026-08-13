import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

interface WhatsAppConfig {
  isConfigured: boolean;
  wabaId?: string;
  phoneNumberId?: string;
  isActive?: boolean;
}

interface SaveConfigRequest {
  wabaId: string;
  phoneNumberId: string;
  accessToken: string;
}

interface TestConnectionResult {
  success: boolean;
  message: string;
}

const API_BASE = '/api/integrations/whatsapp';

async function fetchConfig(): Promise<WhatsAppConfig> {
  const response = await fetch(API_BASE, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch config');
  return response.json();
}

async function saveConfig(request: SaveConfigRequest): Promise<WhatsAppConfig> {
  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error('Failed to save config');
  return response.json();
}

async function testConnection(): Promise<TestConnectionResult> {
  const response = await fetch(`${API_BASE}/test-connection`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to test connection');
  return response.json();
}

function getCsrfToken(): string {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta?.getAttribute('content') ?? '';
}

export function WhatsAppConfigPage() {
  const queryClient = useQueryClient();
  const [wabaId, setWabaId] = useState('');
  const [phoneNumberId, setPhoneNumberId] = useState('');
  const [accessToken, setAccessToken] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<TestConnectionResult | null>(null);

  const { data: config, isLoading } = useQuery({
    queryKey: ['whatsapp-config'],
    queryFn: fetchConfig,
  });

  useEffect(() => {
    if (config) {
      setWabaId(config.wabaId || '');
      setPhoneNumberId(config.phoneNumberId || '');
    }
  }, [config]);

  const saveMutation = useMutation({
    mutationFn: saveConfig,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['whatsapp-config'] });
      setAccessToken('');
      setError(null);
    },
    onError: () => setError('Failed to save configuration'),
  });

  const testMutation = useMutation({
    mutationFn: testConnection,
    onSuccess: (data) => {
      setTestResult(data);
      setError(null);
    },
    onError: () => setError('Failed to test connection'),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setTestResult(null);

    if (!wabaId || !phoneNumberId) {
      setError('WABA ID and Phone Number ID are required.');
      return;
    }

    if (!config?.isConfigured && !accessToken) {
      setError('Access Token is required for initial configuration.');
      return;
    }

    saveMutation.mutate({
      wabaId,
      phoneNumberId,
      accessToken: accessToken || '',
    });
  };

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="p-6 max-w-2xl">
      <h1 className="text-2xl font-bold mb-6">WhatsApp Configuration</h1>

      {config?.isConfigured && (
        <div className="mb-6 p-4 bg-green-50 border border-green-200 rounded">
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium text-green-800">WhatsApp is configured</p>
              <p className="text-sm text-green-600">
                Status: {config.isActive ? 'Active' : 'Inactive'}
              </p>
            </div>
            <button
              onClick={() => testMutation.mutate()}
              disabled={testMutation.isPending}
              className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 disabled:opacity-50"
            >
              {testMutation.isPending ? 'Testing...' : 'Test Connection'}
            </button>
          </div>
          {testResult && (
            <div
              className={`mt-3 p-3 rounded ${
                testResult.success
                  ? 'bg-green-100 text-green-800'
                  : 'bg-red-100 text-red-800'
              }`}
            >
              {testResult.message}
            </div>
          )}
        </div>
      )}

      {error && (
        <div className="mb-4 p-3 bg-red-100 border border-red-400 text-red-700 rounded">
          {error}
        </div>
      )}

      <div className="bg-yellow-50 border border-yellow-200 rounded p-4 mb-6">
        <p className="text-yellow-800 text-sm">
          <strong>Important:</strong> The access token is stored securely and will never be
          displayed again. You will need to re-enter it if you want to update the configuration.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">
            WABA ID (WhatsApp Business Account ID) *
          </label>
          <input
            type="text"
            value={wabaId}
            onChange={(e) => setWabaId(e.target.value)}
            required
            className="w-full border rounded px-3 py-2"
            placeholder="e.g., 1234567890123456"
          />
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">Phone Number ID *</label>
          <input
            type="text"
            value={phoneNumberId}
            onChange={(e) => setPhoneNumberId(e.target.value)}
            required
            className="w-full border rounded px-3 py-2"
            placeholder="e.g., 1234567890123456"
          />
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">
            Access Token {config?.isConfigured ? '(leave empty to keep current)' : '*'}
          </label>
          <input
            type="password"
            value={accessToken}
            onChange={(e) => setAccessToken(e.target.value)}
            required={!config?.isConfigured}
            className="w-full border rounded px-3 py-2"
            placeholder="Enter your WhatsApp Cloud API access token"
          />
        </div>

        <div className="flex gap-4">
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="bg-blue-600 text-white px-6 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {saveMutation.isPending ? 'Saving...' : 'Save Configuration'}
          </button>
        </div>
      </form>

      <div className="mt-8 p-4 bg-gray-50 rounded">
        <h3 className="font-medium mb-2">How to get these values:</h3>
        <ol className="list-decimal list-inside text-sm text-gray-600 space-y-2">
          <li>Go to the Meta Business Suite</li>
          <li>Navigate to Settings &gt; Business Accounts</li>
          <li>Select your WhatsApp Business Account</li>
          <li>Copy the WABA ID from the account details</li>
          <li>Go to WhatsApp &gt; API Setup</li>
          <li>Copy the Phone Number ID</li>
          <li>Generate or copy your Access Token</li>
        </ol>
      </div>
    </div>
  );
}
