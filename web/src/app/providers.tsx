import { GoogleOAuthProvider } from '@react-oauth/google';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { AuthProvider, googleClientId } from '../features/auth';
import { ApiError } from '../shared/api/http';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // 4xx 是我們自己的業務錯誤，重試沒有意義；5xx 才可能是暫時性的
      // （Azure SQL 冷啟動，設計文件 09 第 2 節）
      retry: (failureCount, error) =>
        error instanceof ApiError && error.status >= 500 && failureCount < 2,
    },
  },
});

/**
 * 沒設定 Client ID 就不掛 Google 的 Provider——它會去載入 Google 的 script，
 * 沒有 ID 只會在 console 留下一堆錯誤。Email 登入不受影響。
 */
function GoogleIdentity({ children }: { children: ReactNode }) {
  if (!googleClientId) return <>{children}</>;
  return (
    <GoogleOAuthProvider clientId={googleClientId}>
      {children}
    </GoogleOAuthProvider>
  );
}

export function Providers({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {/* AuthProvider 要在 QueryClientProvider 之內：登出時要清私人快取 */}
      <GoogleIdentity>
        <AuthProvider>{children}</AuthProvider>
      </GoogleIdentity>
    </QueryClientProvider>
  );
}
