import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { Spinner } from '../../shared/ui/States';
import { useAuth } from './authContext';

/**
 * 需要登入才能看的頁面包這個。
 *
 * 三個狀態要分清楚：**還在確認**（不要急著導走，重新整理時會閃一下登入頁）、
 * **沒登入**（記住現在的路徑再導去登入）、**已登入**（正常顯示）。
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, isRestoring } = useAuth();
  const location = useLocation();

  if (isRestoring) return <Spinner label="確認登入狀態…" />;

  if (!user) {
    return <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />;
  }

  return <>{children}</>;
}
