import type { PagedResult } from '../../../shared/api/PagedResult';
import { apiGet, apiPatch, apiPost } from '../../../shared/api/http';
import type { AdminOrderDto } from '../model/AdminOrderDto';
import type { DashboardDto } from '../model/DashboardDto';
import type { PauseSalesResult } from '../model/PauseSalesResult';
import type { ResetResult } from '../model/ResetResult';

/**
 * 後台的兩個寫入操作跟購票一樣要帶 `Idempotency-Key`，
 * 但去重紀錄存在**不會被重置刪除的** `AdminAudits`（設計文件 14 第 3 節）。
 */
export const adminApi = {
  dashboard: (signal?: AbortSignal) =>
    apiGet<DashboardDto>('/admin/dashboard', signal),

  orders: (performanceId: number | null, signal?: AbortSignal) =>
    apiGet<PagedResult<AdminOrderDto>>(
      `/admin/orders${performanceId ? `?performanceId=${performanceId}` : ''}`,
      signal,
    ),

  setSalesPaused: (
    performanceId: number,
    isSalesPaused: boolean,
    key: string,
  ) =>
    apiPatch<PauseSalesResult>(
      `/admin/performances/${performanceId}`,
      { isSalesPaused },
      {
        headers: { 'Idempotency-Key': key },
      },
    ),

  reset: (key: string) =>
    apiPost<ResetResult>(
      '/admin/reset',
      { confirmation: 'RESET' },
      {
        headers: { 'Idempotency-Key': key },
      },
    ),
};
