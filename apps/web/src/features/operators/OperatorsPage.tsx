import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

interface Operator {
  id: string;
  userId: string;
  email: string;
  displayName?: string;
  status: string;
  createdAt: string;
  deactivatedAt?: string;
  reactivatedAt?: string;
}

interface InviteOperatorRequest {
  email: string;
  displayName?: string;
}

interface InviteOperatorResponse {
  invitationId: string;
  email: string;
  activationLink: string;
  message: string;
}

const API_BASE = '/api/operators';

async function fetchOperators(): Promise<Operator[]> {
  const response = await fetch(API_BASE, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch operators');
  return response.json();
}

async function inviteOperator(request: InviteOperatorRequest): Promise<InviteOperatorResponse> {
  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error('Failed to invite operator');
  return response.json();
}

async function deactivateOperator(operatorId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${operatorId}/deactivate`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to deactivate operator');
}

async function reactivateOperator(operatorId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${operatorId}/reactivate`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to reactivate operator');
}

async function resendInvite(operatorId: string): Promise<InviteOperatorResponse> {
  const response = await fetch(`${API_BASE}/${operatorId}/resend-invite`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to resend invite');
  return response.json();
}

function getCsrfToken(): string {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta?.getAttribute('content') ?? '';
}

export function OperatorsPage() {
  const queryClient = useQueryClient();
  const [showInviteForm, setShowInviteForm] = useState(false);
  const [activationLink, setActivationLink] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data: operators, isLoading } = useQuery({
    queryKey: ['operators'],
    queryFn: fetchOperators,
  });

  const inviteMutation = useMutation({
    mutationFn: inviteOperator,
    onSuccess: (data) => {
      setActivationLink(data.activationLink);
      setShowInviteForm(false);
      queryClient.invalidateQueries({ queryKey: ['operators'] });
    },
    onError: () => setError('Failed to invite operator'),
  });

  const deactivateMutation = useMutation({
    mutationFn: deactivateOperator,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
    onError: () => setError('Failed to deactivate operator'),
  });

  const reactivateMutation = useMutation({
    mutationFn: reactivateOperator,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
    onError: () => setError('Failed to reactivate operator'),
  });

  const resendMutation = useMutation({
    mutationFn: resendInvite,
    onSuccess: (data) => {
      setActivationLink(data.activationLink);
      queryClient.invalidateQueries({ queryKey: ['operators'] });
    },
    onError: () => setError('Failed to resend invite'),
  });

  const handleInvite = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    inviteMutation.mutate({
      email: formData.get('email') as string,
      displayName: formData.get('displayName') as string || undefined,
    });
  };

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="p-6">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Operator Management</h1>
        <button
          onClick={() => setShowInviteForm(true)}
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
        >
          Invite Operator
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
          <p className="font-bold">Invitation created successfully!</p>
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

      {showInviteForm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h2 className="text-xl font-bold mb-4">Invite New Operator</h2>
            <form onSubmit={handleInvite}>
              <div className="mb-4">
                <label className="block text-sm font-medium mb-1">Email *</label>
                <input
                  name="email"
                  type="email"
                  required
                  className="w-full border rounded px-3 py-2"
                />
              </div>
              <div className="mb-4">
                <label className="block text-sm font-medium mb-1">Display Name</label>
                <input
                  name="displayName"
                  type="text"
                  className="w-full border rounded px-3 py-2"
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowInviteForm(false)}
                  className="px-4 py-2 border rounded"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={inviteMutation.isPending}
                  className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
                >
                  {inviteMutation.isPending ? 'Inviting...' : 'Invite'}
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
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {operators?.map((op) => (
              <tr key={op.id}>
                <td className="px-6 py-4 whitespace-nowrap">{op.email}</td>
                <td className="px-6 py-4 whitespace-nowrap">{op.displayName || '-'}</td>
                <td className="px-6 py-4 whitespace-nowrap">
                  <span
                    className={`px-2 py-1 rounded text-xs font-medium ${
                      op.status === 'Active'
                        ? 'bg-green-100 text-green-800'
                        : op.status === 'Pending'
                        ? 'bg-yellow-100 text-yellow-800'
                        : 'bg-red-100 text-red-800'
                    }`}
                  >
                    {op.status}
                  </span>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm space-x-2">
                  {op.status === 'Active' && (
                    <button
                      onClick={() => deactivateMutation.mutate(op.id)}
                      className="text-red-600 hover:text-red-900"
                    >
                      Deactivate
                    </button>
                  )}
                  {op.status === 'Inactive' && (
                    <button
                      onClick={() => reactivateMutation.mutate(op.id)}
                      className="text-green-600 hover:text-green-900"
                    >
                      Reactivate
                    </button>
                  )}
                  {op.status === 'Pending' && (
                    <button
                      onClick={() => resendMutation.mutate(op.id)}
                      className="text-blue-600 hover:text-blue-900"
                    >
                      Resend Invite
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
