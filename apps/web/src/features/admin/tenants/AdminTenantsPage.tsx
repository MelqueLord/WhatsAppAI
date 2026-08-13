import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

interface Tenant {
  id: string;
  name: string;
  status: string;
  createdAt: string;
  suspendedAt?: string;
  reactivatedAt?: string;
  suspensionReason?: string;
}

interface CreateTenantRequest {
  name: string;
  ownerEmail: string;
  ownerDisplayName?: string;
}

interface CreateTenantResponse {
  tenantId: string;
  tenantName: string;
  ownerEmail: string;
  activationLink: string;
  message: string;
}

const API_BASE = '/api/admin/tenants';

async function fetchTenants(): Promise<Tenant[]> {
  const response = await fetch(API_BASE, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch tenants');
  return response.json();
}

async function createTenant(request: CreateTenantRequest): Promise<CreateTenantResponse> {
  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error('Failed to create tenant');
  return response.json();
}

async function suspendTenant(tenantId: string, reason: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${tenantId}/suspend`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
    body: JSON.stringify({ reason }),
  });
  if (!response.ok) throw new Error('Failed to suspend tenant');
}

async function reactivateTenant(tenantId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${tenantId}/reactivate`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to reactivate tenant');
}

function getCsrfToken(): string {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta?.getAttribute('content') ?? '';
}

export function AdminTenantsPage() {
  const queryClient = useQueryClient();
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [activationLink, setActivationLink] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data: tenants, isLoading } = useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: fetchTenants,
  });

  const createMutation = useMutation({
    mutationFn: createTenant,
    onSuccess: (data) => {
      setActivationLink(data.activationLink);
      setShowCreateForm(false);
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
    },
    onError: () => setError('Failed to create tenant'),
  });

  const suspendMutation = useMutation({
    mutationFn: ({ tenantId, reason }: { tenantId: string; reason: string }) =>
      suspendTenant(tenantId, reason),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] }),
    onError: () => setError('Failed to suspend tenant'),
  });

  const reactivateMutation = useMutation({
    mutationFn: reactivateTenant,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] }),
    onError: () => setError('Failed to reactivate tenant'),
  });

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    createMutation.mutate({
      name: formData.get('name') as string,
      ownerEmail: formData.get('ownerEmail') as string,
      ownerDisplayName: formData.get('ownerDisplayName') as string || undefined,
    });
  };

  const handleSuspend = (tenantId: string) => {
    const reason = prompt('Reason for suspension:');
    if (reason) {
      suspendMutation.mutate({ tenantId, reason });
    }
  };

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="p-6">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Tenant Management</h1>
        <button
          onClick={() => setShowCreateForm(true)}
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
        >
          Create Tenant
        </button>
      </div>

      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
          {error}
          <button onClick={() => setError(null)} className="float-right font-bold">×</button>
        </div>
      )}

      {activationLink && (
        <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded mb-4">
          <p className="font-bold">Tenant created successfully!</p>
          <p className="mt-2">Activation link (save this, it won't be shown again):</p>
          <code className="block mt-2 p-2 bg-green-50 break-all">{activationLink}</code>
          <button
            onClick={() => {
              navigator.clipboard.writeText(activationLink);
            }}
            className="mt-2 bg-green-600 text-white px-3 py-1 rounded text-sm"
          >
            Copy Link
          </button>
          <button
            onClick={() => setActivationLink(null)}
            className="mt-2 ml-2 text-green-700 underline text-sm"
          >
            Dismiss
          </button>
        </div>
      )}

      {showCreateForm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h2 className="text-xl font-bold mb-4">Create New Tenant</h2>
            <form onSubmit={handleCreate}>
              <div className="mb-4">
                <label className="block text-sm font-medium mb-1">Tenant Name *</label>
                <input
                  name="name"
                  type="text"
                  required
                  className="w-full border rounded px-3 py-2"
                />
              </div>
              <div className="mb-4">
                <label className="block text-sm font-medium mb-1">Owner Email *</label>
                <input
                  name="ownerEmail"
                  type="email"
                  required
                  className="w-full border rounded px-3 py-2"
                />
              </div>
              <div className="mb-4">
                <label className="block text-sm font-medium mb-1">Owner Display Name</label>
                <input
                  name="ownerDisplayName"
                  type="text"
                  className="w-full border rounded px-3 py-2"
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowCreateForm(false)}
                  className="px-4 py-2 border rounded"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
                >
                  {createMutation.isPending ? 'Creating...' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="bg-white shadow rounded-lg overflow-hidden">
        <table className="min-w-full">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Created</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {tenants?.map((tenant) => (
              <tr key={tenant.id}>
                <td className="px-6 py-4 whitespace-nowrap">{tenant.name}</td>
                <td className="px-6 py-4 whitespace-nowrap">
                  <span
                    className={`px-2 py-1 rounded text-xs font-medium ${
                      tenant.status === 'Active'
                        ? 'bg-green-100 text-green-800'
                        : 'bg-red-100 text-red-800'
                    }`}
                  >
                    {tenant.status}
                  </span>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {new Date(tenant.createdAt).toLocaleDateString()}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm">
                  {tenant.status === 'Active' ? (
                    <button
                      onClick={() => handleSuspend(tenant.id)}
                      className="text-red-600 hover:text-red-900"
                    >
                      Suspend
                    </button>
                  ) : (
                    <button
                      onClick={() => reactivateMutation.mutate(tenant.id)}
                      className="text-green-600 hover:text-green-900"
                    >
                      Reactivate
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
