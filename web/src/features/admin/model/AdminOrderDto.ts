export interface AdminOrderDto {
  id: string;
  holdId: string;
  performanceId: number;
  eventTitle: string;
  startsAtUtc: string;
  buyerDisplayName: string;
  quantity: number;
  totalAmount: number;
  currency: string;
  createdAtUtc: string;
}
