import { apiGet } from '../../shared/api/http';
import type {
  OrderDto,
  OrderSummaryDto,
  PagedResult,
} from '../../shared/api/types';

export const ordersApi = {
  list: (page = 1, signal?: AbortSignal) =>
    apiGet<PagedResult<OrderSummaryDto>>(
      `/orders?page=${page}&pageSize=10`,
      signal,
    ),

  get: (orderId: string, signal?: AbortSignal) =>
    apiGet<OrderDto>(`/orders/${orderId}`, signal),
};
