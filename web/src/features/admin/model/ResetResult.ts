export interface ResetResult {
  ordersDeleted: number;
  holdsDeleted: number;
  seatsReleased: number;
  earliestPerformanceUtc: string;
}
