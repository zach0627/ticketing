import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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

export function Providers({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
