import type { SeatStatus } from './SeatStatus';

export interface SeatDto {
  id: number;
  sectionId: number;
  rowNumber: number;
  seatNumber: number;
  status: SeatStatus;
}
