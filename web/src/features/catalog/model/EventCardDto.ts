import type { EventCategory } from './EventCategory';
import type { SalesStatus } from './SalesStatus';

export interface EventCardDto {
  id: number;
  code: string;
  category: EventCategory;
  title: string;
  performer: string;
  imagePath: string;
  city: string;
  performanceId: number;
  startsAtUtc: string;
  minPrice: number;
  currency: string;
  salesStatus: SalesStatus;
  serverNowUtc: string;
}
