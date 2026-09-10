import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { useAuth } from '../auth/authContext';
import { bookingApi } from './api';
import { ApiError } from '../../shared/api/http';
import {
  newIdempotencyKey,
  pendingOperations,
} from '../../shared/api/idempotency';
import type { CreateHoldRequest } from '../../shared/api/types';

/** 保留寫入與恢復流程；畫面只提供經選位規則驗證的 payload。 */
export function useReservation(
  id: number,
  refresh: () => Promise<unknown>,
  onRejected: () => void,
) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [restored] = useState(() =>
    user ? pendingOperations.read(user.id, 'hold', String(id)) : null,
  );
  const [error, setError] = useState<string | null>(
    restored ? '上次保留的結果尚未確認，請按「確認保留結果」繼續。' : null,
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [uncertain, setUncertain] = useState(!!restored);
  const submitting = useRef(false);
  const pendingRef = useRef<{ key: string; payload: CreateHoldRequest } | null>(
    restored
      ? { key: restored.key, payload: restored.payload as CreateHoldRequest }
      : null,
  );
  useEffect(() => {
    if (!user || !Number.isFinite(id)) return;
    const controller = new AbortController();
    bookingApi
      .myHolds(id, controller.signal)
      .then((result) => {
        if (result.items[0])
          navigate(`/holds/${result.items[0].id}`, { replace: true });
      })
      .catch(() => {
        /* 寫入時仍會檢查既有保留。 */
      });
    return () => controller.abort();
  }, [user, id, navigate]);

  async function reserve(payload: CreateHoldRequest) {
    if (submitting.current) return;
    if (!user) {
      navigate('/login', {
        state: {
          from: `${location.pathname}${location.search}`,
          seatDraft: { performanceId: id, ...payload },
        },
      });
      return;
    }
    const pending = pendingRef.current ?? {
      key: newIdempotencyKey(),
      payload,
    };
    pendingRef.current = pending;
    pendingOperations.save({
      userId: user.id,
      operation: 'hold',
      target: String(id),
      ...pending,
    });
    submitting.current = true;
    setIsSubmitting(true);
    setError(null);
    try {
      const hold = await bookingApi.createHold(
        id,
        pending.payload,
        pending.key,
      );
      pendingOperations.clear();
      pendingRef.current = null;
      navigate(`/holds/${hold.id}`);
    } catch (caught) {
      if (
        caught instanceof ApiError &&
        caught.code === 'ActiveHoldExists' &&
        caught.holdId
      ) {
        pendingOperations.clear();
        navigate(`/holds/${caught.holdId}`, { replace: true });
        return;
      }
      const unknownResult =
        !(caught instanceof ApiError) ||
        caught.status >= 500 ||
        caught.status === 429 ||
        caught.status === 401;
      setUncertain(unknownResult);
      if (!unknownResult) {
        pendingOperations.clear();
        pendingRef.current = null;
        onRejected();
      }
      setError(
        unknownResult
          ? '連線中斷，保留結果尚未確認。請確認結果，系統會接續同一筆保留。'
          : caught instanceof Error
            ? caught.message
            : '保留失敗，請再試一次。',
      );
      await refresh();
    } finally {
      submitting.current = false;
      setIsSubmitting(false);
    }
  }

  return { user, error, setError, isSubmitting, uncertain, reserve };
}
