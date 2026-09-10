import type { OrderItemDto } from './OrderItemDto';

export interface OrderDto {
  id: string;
  holdId: string;
  eventTitle: string;
  startsAtUtc: string;
  currency: string;
  totalAmount: number;
  createdAtUtc: string;
  items: OrderItemDto[];
}
