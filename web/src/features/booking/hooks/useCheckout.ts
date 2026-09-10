import { useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { ApiError } from '../../../shared/api/http';
import { newIdempotencyKey } from '../../../shared/api/idempotency';
import { useAuth } from '../../auth';
import { bookingApi } from '../api/bookingApi';
import { pendingOperations } from '../api/pendingOperations';

/** 付款與取消共用同一寫入閘門；結果未明時只允許沿用原請求查明結果。 */
export function useCheckout(
  holdId: string,
  performanceId: number | undefined,
  refresh: () => Promise<unknown>,
) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(
    () => !!user && !!pendingOperations.read(user.id, 'checkout', holdId),
  );
  const busyRef = useRef(false);
  const operation = useRef<{
    key: string;
    payload: 'Succeeded' | 'Failed';
  } | null>(null);
  async function pay(outcome: 'Succeeded' | 'Failed') {
    if (!user || busyRef.current) return;
    const restored = pendingOperations.read(user.id, 'checkout', holdId);
    const pending =
      operation.current ??
      (restored
        ? {
            key: restored.key,
            payload: restored.payload as 'Succeeded' | 'Failed',
          }
        : { key: newIdempotencyKey(), payload: outcome });
    operation.current = pending;
    pendingOperations.save({
      userId: user.id,
      operation: 'checkout',
      target: holdId,
      ...pending,
    });
    busyRef.current = true;
    setError(null);
    setBusy(true);
    try {
      const order = await bookingApi.checkout(
        holdId,
        pending.payload,
        pending.key,
      );
      pendingOperations.clear();
      operation.current = null;
      await queryClient.invalidateQueries({ queryKey: ['orders'] });
      navigate(`/orders/${order.id}`, { state: { purchased: true } });
    } catch (caught) {
      const unknownResult =
        !(caught instanceof ApiError) ||
        caught.status >= 500 ||
        caught.status === 429 ||
        caught.status === 401;
      setUncertain(unknownResult);
      if (!unknownResult) {
        pendingOperations.clear();
        operation.current = null;
      }
      setError(
        unknownResult
          ? '尚未確認付款結果。請按「確認付款結果」，接續同一筆交易。'
          : caught instanceof ApiError
            ? caught.message
            : '付款失敗，請再試一次。',
      );
      await refresh();
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }

  async function cancel() {
    if (busyRef.current || uncertain) return;
    busyRef.current = true;
    setBusy(true);
    setError(null);
    try {
      await bookingApi.cancel(holdId);
      await queryClient.invalidateQueries({
        queryKey: ['seatmap', performanceId],
      });
      navigate(`/performances/${performanceId}/seats`, { replace: true });
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : '取消失敗，請再試一次。',
      );
      await refresh();
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }

  return { error, busy, uncertain, pay, cancel };
}
