import type { ReactNode } from 'react';
import { useAuthSession } from '../hooks/useAuthSession';
import { AuthContext } from './authContext';

export function AuthProvider({ children }: { children: ReactNode }) {
  const value = useAuthSession();
  return <AuthContext value={value}>{children}</AuthContext>;
}
