import { apiGet } from '../../shared/api/http';
import type { EventCardDto, EventDetailDto, PagedResult, SeatMapDto } from '../../shared/api/types';

export const catalogApi = {
  listEvents: (category?: string, signal?: AbortSignal) =>
    apiGet<PagedResult<EventCardDto>>(
      `/events?pageSize=30${category ? `&category=${category}` : ''}`,
      signal,
    ),

  getEvent: (code: string, signal?: AbortSignal) =>
    apiGet<EventDetailDto>(`/events/${code}`, signal),

  getSeatMap: (performanceId: number, signal?: AbortSignal) =>
    apiGet<SeatMapDto>(`/performances/${performanceId}/seats`, signal),
};
