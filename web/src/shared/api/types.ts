// 後端 DTO 的形狀。階段 4 先手寫最小集合；
// 之後改由 openapi-typescript 從 /openapi/v1.json 產生（設計文件 13 第 6 節）。

export type EventCategory = 'Concert' | 'Sport';
export type SalesStatus = 'NotYetOnSale' | 'OnSale' | 'SalesClosed' | 'Paused';
export type SeatStatus = 'Available' | 'Held' | 'Sold';

export interface PagedResult<T> {
  items: T[];
  total: number;
}

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

export interface SectionDto {
  id: number;
  code: string;
  name: string;
  price: number;
  rowCount: number;
  seatsPerRow: number;
}

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

export interface SeatDto {
  id: number;
  sectionId: number;
  rowNumber: number;
  seatNumber: number;
  status: SeatStatus;
}

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

/** 後端的 ProblemDetails（RFC 7807）＋ 我們加的兩個欄位。 */
export interface ApiProblem {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
}

// ── 使用者與登入（階段 5）──

export type UserRole = 'Customer' | 'Admin';

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  user: UserDto;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}
