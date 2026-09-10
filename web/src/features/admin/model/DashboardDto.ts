import type { AdminPerformanceDto } from './AdminPerformanceDto';

export interface DashboardDto {
  activeHolds: number;
  soldSeats: number;
  orders: number;
  serverNowUtc: string;
  performances: AdminPerformanceDto[];
}
