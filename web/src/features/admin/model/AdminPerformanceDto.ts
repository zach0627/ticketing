import type { SalesStatus } from '../../catalog';

export interface AdminPerformanceDto {
  id: number;
  eventCode: string;
  eventTitle: string;
  startsAtUtc: string;
  isSalesPaused: boolean;
  salesStatus: SalesStatus;
}
