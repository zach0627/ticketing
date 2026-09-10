import type { PagedResult } from '../../../shared/api/PagedResult';
import { apiGet } from '../../../shared/api/http';
import type { OrderDto } from '../model/OrderDto';
import type { OrderSummaryDto } from '../model/OrderSummaryDto';

export const ordersApi = {
  list: (page = 1, signal?: AbortSignal) =>
    apiGet<PagedResult<OrderSummaryDto>>(
      `/orders?page=${page}&pageSize=10`,
      signal,
    ),

  get: (orderId: string, signal?: AbortSignal) =>
    apiGet<OrderDto>(`/orders/${orderId}`, signal),
};
