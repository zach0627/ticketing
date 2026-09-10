import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { catalogQueries } from '../../catalog';
import { seatPollingInterval, shouldPollSeats } from '../model/polling';

/** 背景分頁或十分鐘沒有操作時停止輪詢；返回操作時重新取得座位狀態。 */
export function useSeatMap(performanceId: number) {
  const lastActivity = useRef<number | null>(null);
  const query = useQuery({
    ...catalogQueries.seatMap(performanceId),
    refetchInterval: () =>
      lastActivity.current === null ||
      shouldPollSeats(lastActivity.current, Date.now())
        ? seatPollingInterval
        : false,
    refetchIntervalInBackground: false,
  });
  const { refetch } = query;
  useEffect(() => {
    function resume() {
      const now = Date.now();
      const wasIdle =
        lastActivity.current !== null &&
        !shouldPollSeats(lastActivity.current, now);
      lastActivity.current = now;
      if (wasIdle) void refetch();
    }
    resume();
    window.addEventListener('pointerdown', resume, { passive: true });
    window.addEventListener('keydown', resume);
    window.addEventListener('focus', resume);
    return () => {
      window.removeEventListener('pointerdown', resume);
      window.removeEventListener('keydown', resume);
      window.removeEventListener('focus', resume);
    };
  }, [refetch]);
  return query;
}
