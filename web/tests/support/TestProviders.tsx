import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { vi } from 'vitest';
import { AuthContext } from '../../src/features/auth/context/authContext';
import type { AuthContextValue } from '../../src/features/auth/context/authContext';

export const buyer = {
  id: 'buyer-a',
  email: 'buyer@example.test',
  displayName: 'Buyer',
  role: 'Customer' as const,
};

export function testProviders({
  entry = '/performances/1/seats',
  path = '*',
  user = buyer,
  state = undefined,
}: {
  entry?: string;
  path?: string;
  user?: AuthContextValue['user'];
  state?: unknown;
} = {}) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const auth: AuthContextValue = {
    user,
    isRestoring: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  };
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={client}>
        <AuthContext value={auth}>
          <MemoryRouter initialEntries={[{ pathname: entry, state }]}>
            <Routes>
              <Route path={path} element={children} />
              {path !== '*' && <Route path="*" element={children} />}
            </Routes>
          </MemoryRouter>
        </AuthContext>
      </QueryClientProvider>
    );
  }
  return { wrapper: Wrapper, client, auth };
}
