import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { adminApi } from '../api/adminApi';

import { useAdminActions } from './useAdminActions';

export function useAdminDashboard() {
  const [performanceId, setPerformanceId] = useState<number | null>(null);
  const dashboard = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => adminApi.dashboard(signal),
  });

  const orders = useQuery({
    queryKey: ['admin', 'orders', performanceId],
    queryFn: ({ signal }) => adminApi.orders(performanceId, signal),
  });

  const actions = useAdminActions();
  return { dashboard, orders, performanceId, setPerformanceId, ...actions };
}
