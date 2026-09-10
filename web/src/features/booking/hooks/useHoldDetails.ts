import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { useParams } from 'react-router';
import { bookingApi } from '../api/bookingApi';
import { useCheckout } from './useCheckout';
import { useCountdown } from './useCountdown';

import { catalogQueries } from '../../catalog';

export function useHoldDetails() {
  const { holdId = '' } = useParams();
  const query = useQuery({
    queryKey: ['hold', holdId],
    queryFn: ({ signal }) => bookingApi.getHold(holdId, signal),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });

  const { data, refetch } = query;
  const { error, busy, uncertain, pay, cancel } = useCheckout(
    holdId,
    data?.performanceId,
    refetch,
  );
  const seatmap = useQuery({
    ...catalogQueries.seatMap(data?.performanceId),
    enabled: !!data,
  });
  const event = useQuery({
    ...catalogQueries.event(seatmap.data?.eventCode),
    enabled: !!seatmap.data,
  });
  const remaining = useCountdown(
    data?.serverNowUtc ?? '',
    data?.expiresAtUtc ?? '',
  );
  const expiryChecked = useRef(false);
  useEffect(() => {
    if (
      data?.status === 'Active' &&
      remaining === 0 &&
      !expiryChecked.current
    ) {
      expiryChecked.current = true;
      void refetch();
    }
  }, [data?.status, remaining, refetch]);

  return {
    query,
    error,
    busy,
    uncertain,
    pay,
    cancel,
    event: event.data,
    remaining,
  };
}
