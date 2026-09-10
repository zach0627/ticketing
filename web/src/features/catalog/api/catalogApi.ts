import type { PagedResult } from '../../../shared/api/PagedResult';
import { apiGet } from '../../../shared/api/http';
import type { EventCardDto } from '../model/EventCardDto';
import type { EventDetailDto } from '../model/EventDetailDto';
import type { SeatMapDto } from '../model/SeatMapDto';

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
