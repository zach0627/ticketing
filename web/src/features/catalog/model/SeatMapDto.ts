import type { EventCategory } from './EventCategory';
import type { SalesStatus } from './SalesStatus';
import type { SeatDto } from './SeatDto';
import type { SectionDto } from './SectionDto';

export interface SeatMapDto {
  performanceId: number;
  eventCode: string;
  eventTitle: string;
  category: EventCategory;
  maxTicketsPerBuyer: number;
  allowsContiguousAllocation: boolean;
  salesStatus: SalesStatus;
  serverNowUtc: string;
  sections: SectionDto[];
  seats: SeatDto[];
}
