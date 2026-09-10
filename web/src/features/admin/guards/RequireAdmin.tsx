import type { ReactNode } from 'react';
import { Navigate } from 'react-router';
import { Spinner } from '../../../shared/ui/States';
import { useAuth } from '../../auth';

/**
 * 只有管理者看得到的頁面。
 *
 * **這只是 UX**：真正的把關是後端的 `[Authorize(Roles = "Admin")]`。
 * 前端藏起來只是不讓一般使用者看到一堆會回 403 的按鈕（設計文件 14 第 1 節）。
 */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { user, isRestoring } = useAuth();

  if (isRestoring) return <Spinner label="確認登入狀態…" />;
  if (!user) return <Navigate to="/login" replace state={{ from: '/admin' }} />;
  if (user.role !== 'Admin') return <Navigate to="/" replace />;

  return <>{children}</>;
}
