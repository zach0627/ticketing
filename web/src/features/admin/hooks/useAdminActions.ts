import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ApiError } from '../../../shared/api/http';
import { newIdempotencyKey } from '../../../shared/api/idempotency';
import { formatTaipei } from '../../../shared/utils/format';
import { adminApi } from '../api/adminApi';

export function useAdminActions() {
  const queryClient = useQueryClient();
  const [confirmation, setConfirmation] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function refresh() {
    return queryClient.invalidateQueries({ queryKey: ['admin'] });
  }

  const togglePause = useMutation({
    mutationFn: ({ id, paused }: { id: number; paused: boolean }) =>
      adminApi.setSalesPaused(id, paused, newIdempotencyKey()),
    onSuccess: async (result) => {
      setError(null);
      setMessage(
        `場次 ${result.performanceId} 已${result.isSalesPaused ? '暫停' : '恢復'}售票。`,
      );
      await refresh();
    },
    onError: (caught: unknown) =>
      setError(
        caught instanceof ApiError ? caught.message : '操作失敗，請再試一次。',
      ),
  });

  const reset = useMutation({
    mutationFn: () => adminApi.reset(newIdempotencyKey()),
    onSuccess: async (result) => {
      setError(null);
      setConfirmation('');
      setMessage(
        `重置完成：刪除 ${result.ordersDeleted} 張訂單、${result.holdsDeleted} 筆保留，` +
          `釋放 ${result.seatsReleased} 個座位；最早的一場現在是 ${formatTaipei(result.earliestPerformanceUtc)}。`,
      );
      // 重置會刪掉所有購買資料，別人的訂單頁會開始回 404——先把整個快取清掉
      queryClient.clear();
      await refresh();
    },
    onError: (caught: unknown) =>
      setError(
        caught instanceof ApiError ? caught.message : '重置失敗，請再試一次。',
      ),
  });

  const busy = togglePause.isPending || reset.isPending;
  return {
    confirmation,
    setConfirmation,
    message,
    error,
    togglePause,
    reset,
    busy,
  };
}
