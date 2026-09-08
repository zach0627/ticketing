import { apiGet } from '../../shared/api/http';
import type { OrderDto, OrderSummaryDto, PagedResult } from '../../shared/api/types';

export const ordersApi = {
  list: (signal?: AbortSignal) => apiGet<PagedResult<OrderSummaryDto>>('/orders', signal),

  get: (orderId: string, signal?: AbortSignal) => apiGet<OrderDto>(`/orders/${orderId}`, signal),
};
