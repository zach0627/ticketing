import { queryOptions } from '@tanstack/react-query';
import { catalogApi } from './catalogApi';

/** 共用讀取契約；沿用既有 key，避免選位與付款頁產生不同座位快取。 */
export const catalogQueries = {
  events: () =>
    queryOptions({
      queryKey: ['events'],
      queryFn: ({ signal }) => catalogApi.listEvents(undefined, signal),
    }),
  event: (code: string | undefined) =>
    queryOptions({
      queryKey: ['event', code],
      queryFn: ({ signal }) => catalogApi.getEvent(code!, signal),
    }),
  seatMap: (performanceId: number | undefined) =>
    queryOptions({
      queryKey: ['seatmap', performanceId],
      queryFn: ({ signal }) => catalogApi.getSeatMap(performanceId!, signal),
    }),
};
