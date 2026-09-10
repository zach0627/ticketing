import type { EventCategory } from './EventCategory';
import type { SalesStatus } from './SalesStatus';
import type { SectionDto } from './SectionDto';

export interface EventDetailDto {
  id: number;
  code: string;
  category: EventCategory;
  title: string;
  performer: string;
  genre: string;
  description: string;
  imagePath: string;
  performance: {
    id: number;
    city: string;
    venue: string;
    startsAtUtc: string;
    salesOpensAtUtc: string;
    salesClosesAtUtc: string;
    durationMinutes: number;
    isSalesPaused: boolean;
    salesStatus: SalesStatus;
  };
  sections: SectionDto[];
  maxTicketsPerBuyer: number;
  allowsContiguousAllocation: boolean;
  currency: string;
  serverNowUtc: string;
}
