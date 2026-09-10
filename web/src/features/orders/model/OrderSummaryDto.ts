export interface OrderSummaryDto {
  id: string;
  holdId: string;
  eventTitle: string;
  startsAtUtc: string;
  quantity: number;
  totalAmount: number;
  currency: string;
  createdAtUtc: string;
}
