import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

interface WebhookEvent {
  id: string;
  phoneNumberId: string;
  tenantId?: string;
  status: string;
  createdAt: string;
  processedAt?: string;
  retryCount: number;
  errorMessage?: string;
}

const API_BASE = '/api/webhook-events';

async function fetchEvents(status?: string): Promise<WebhookEvent[]> {
  const params = new URLSearchParams();
  if (status) params.append('status', status);
  params.append('limit', '50');

  const response = await fetch(`${API_BASE}?${params}`, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch events');
  return response.json();
}

async function reprocessEvent(eventId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${eventId}/reprocess`, {
    method: 'POST',
    headers: {
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to reprocess event');
}

function getCsrfToken(): string {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta?.getAttribute('content') ?? '';
}

export function WebhookEventsPage() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [error, setError] = useState<string | null>(null);

  const { data: events, isLoading } = useQuery({
    queryKey: ['webhook-events', statusFilter],
    queryFn: () => fetchEvents(statusFilter === 'all' ? undefined : statusFilter),
  });

  const reprocessMutation = useMutation({
    mutationFn: reprocessEvent,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhook-events'] });
      setError(null);
    },
    onError: () => setError('Failed to reprocess event'),
  });

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'processed':
        return 'bg-green-100 text-green-800';
      case 'pending':
        return 'bg-yellow-100 text-yellow-800';
      case 'processing':
        return 'bg-blue-100 text-blue-800';
      case 'failed':
        return 'bg-orange-100 text-orange-800';
      case 'dead':
        return 'bg-red-100 text-red-800';
      case 'unknown':
        return 'bg-gray-100 text-gray-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="p-6">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Webhook Events</h1>
        <div className="flex gap-2">
          {['all', 'pending', 'failed', 'dead', 'unknown'].map((status) => (
            <button
              key={status}
              onClick={() => setStatusFilter(status)}
              className={`px-3 py-1 rounded text-sm ${
                statusFilter === status
                  ? 'bg-blue-600 text-white'
                  : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
              }`}
            >
              {status.charAt(0).toUpperCase() + status.slice(1)}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
          {error}
          <button onClick={() => setError(null)} className="float-right font-bold">×</button>
        </div>
      )}

      <div className="bg-white shadow rounded-lg overflow-hidden">
        <table className="min-w-full">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                ID
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Phone Number ID
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Status
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Created
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Retries
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Error
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {events?.map((event) => (
              <tr key={event.id}>
                <td className="px-6 py-4 whitespace-nowrap text-sm font-mono">
                  {event.id.slice(0, 8)}...
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm">
                  {event.phoneNumberId}
                </td>
                <td className="px-6 py-4 whitespace-nowrap">
                  <span className={`px-2 py-1 rounded text-xs font-medium ${getStatusColor(event.status)}`}>
                    {event.status}
                  </span>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {new Date(event.createdAt).toLocaleString()}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm">
                  {event.retryCount}
                </td>
                <td className="px-6 py-4 text-sm text-red-600 max-w-xs truncate">
                  {event.errorMessage || '-'}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm">
                  {(event.status === 'Failed' || event.status === 'Dead') && (
                    <button
                      onClick={() => reprocessMutation.mutate(event.id)}
                      disabled={reprocessMutation.isPending}
                      className="text-blue-600 hover:text-blue-900 disabled:opacity-50"
                    >
                      Reprocess
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {events?.length === 0 && (
          <div className="text-center py-8 text-gray-500">
            No webhook events found.
          </div>
        )}
      </div>

      <div className="mt-6 p-4 bg-gray-50 rounded">
        <h3 className="font-medium mb-2">Status Legend:</h3>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-2 text-sm">
          <div><span className="font-medium">Pending:</span> Awaiting processing</div>
          <div><span className="font-medium">Processing:</span> Currently being processed</div>
          <div><span className="font-medium">Processed:</span> Successfully processed</div>
          <div><span className="font-medium">Failed:</span> Failed, will retry</div>
          <div><span className="font-medium">Dead:</span> Failed after max retries</div>
          <div><span className="font-medium">Unknown:</span> Unrecognized event type</div>
        </div>
      </div>
    </div>
  );
}
