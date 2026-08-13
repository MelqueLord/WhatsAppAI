import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';

interface InvitationInfo {
  id: string;
  email: string;
  purpose: string;
  isUsable: boolean;
  expiresAt: string;
}

interface ActivateRequest {
  invitationId: string;
  token: string;
  password: string;
}

interface ActivateResponse {
  userId: string;
  email: string;
  tenantId: string;
  role: string;
}

async function fetchInvitationInfo(invitationId: string): Promise<InvitationInfo> {
  const response = await fetch(`/api/auth/activate/invitation/${invitationId}`, {
    credentials: 'include',
  });
  if (!response.ok) throw new Error('Failed to fetch invitation info');
  return response.json();
}

async function activateAccount(request: ActivateRequest): Promise<ActivateResponse> {
  const response = await fetch('/api/auth/activate', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken(),
    },
    credentials: 'include',
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error || 'Failed to activate account');
  }
  return response.json();
}

function getCsrfToken(): string {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta?.getAttribute('content') ?? '';
}

export function ActivatePage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const invitationId = searchParams.get('invitation');
  const token = searchParams.get('token');

  const { data: invitationInfo, isLoading: isLoadingInfo } = useQuery({
    queryKey: ['invitation', invitationId],
    queryFn: () => fetchInvitationInfo(invitationId!),
    enabled: !!invitationId,
  });

  const activateMutation = useMutation({
    mutationFn: activateAccount,
    onSuccess: (data) => {
      navigate('/dashboard');
    },
    onError: (err: Error) => setError(err.message),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!invitationId || !token) {
      setError('Invalid activation link.');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }

    activateMutation.mutate({
      invitationId,
      token,
      password,
    });
  };

  if (!invitationId || !token) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="max-w-md w-full p-6 bg-white rounded-lg shadow">
          <h1 className="text-2xl font-bold text-red-600 mb-4">Invalid Link</h1>
          <p className="text-gray-600">
            This activation link is invalid. Please check the link and try again.
          </p>
        </div>
      </div>
    );
  }

  if (isLoadingInfo) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="text-center">Loading...</div>
      </div>
    );
  }

  if (invitationInfo && !invitationInfo.isUsable) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="max-w-md w-full p-6 bg-white rounded-lg shadow">
          <h1 className="text-2xl font-bold text-red-600 mb-4">Link Expired</h1>
          <p className="text-gray-600">
            This activation link has expired or has already been used.
            Please request a new invitation.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="max-w-md w-full p-6 bg-white rounded-lg shadow">
        <h1 className="text-2xl font-bold mb-4">Activate Your Account</h1>

        {invitationInfo && (
          <div className="mb-4 p-3 bg-blue-50 rounded">
            <p className="text-sm text-blue-800">
              <strong>Email:</strong> {invitationInfo.email}
            </p>
            <p className="text-sm text-blue-800">
              <strong>Role:</strong> {invitationInfo.purpose}
            </p>
          </div>
        )}

        {error && (
          <div className="mb-4 p-3 bg-red-100 border border-red-400 text-red-700 rounded">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div className="mb-4">
            <label className="block text-sm font-medium mb-1">Password *</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              className="w-full border rounded px-3 py-2"
              placeholder="At least 8 characters"
            />
          </div>

          <div className="mb-6">
            <label className="block text-sm font-medium mb-1">Confirm Password *</label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              minLength={8}
              className="w-full border rounded px-3 py-2"
            />
          </div>

          <button
            type="submit"
            disabled={activateMutation.isPending}
            className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {activateMutation.isPending ? 'Activating...' : 'Activate Account'}
          </button>
        </form>
      </div>
    </div>
  );
}
