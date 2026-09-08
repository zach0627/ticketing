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

/**
 * 後端的 ProblemDetails（RFC 7807）＋ 我們加的欄位。
 * `holdId` 只在 `ActiveHoldExists` 時出現：告訴前端該把人導去哪一筆保留。
 */
export interface ApiProblem {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  holdId?: string;
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

// ── 保留與訂單（階段 6）──

export type HoldStatus = 'Active' | 'Completed' | 'Cancelled' | 'Expired';
export type SelectionMode = 'Manual' | 'Contiguous';

export interface HoldItemDto {
  seatId: number;
  sectionCode: string;
  rowNumber: number;
  seatNumber: number;
  unitPrice: number;
}

export interface HoldDto {
  id: string;
  performanceId: number;
  status: HoldStatus;
  orderId: string | null;
  /** 伺服器的「現在」。倒數要用它當基準，不能用瀏覽器時鐘。 */
  serverNowUtc: string;
  expiresAtUtc: string;
  currency: string;
  totalAmount: number;
  items: HoldItemDto[];
}

export interface CreateHoldRequest {
  sectionId: number;
  quantity: number;
  selectionMode: SelectionMode;
  seatIds: number[];
}

export interface OrderItemDto {
  sectionCode: string;
  rowNumber: number;
  seatNumber: number;
  unitPrice: number;
  ticketCode: string;
}

export interface OrderDto {
  id: string;
  holdId: string;
  eventTitle: string;
  startsAtUtc: string;
  currency: string;
  totalAmount: number;
  createdAtUtc: string;
  items: OrderItemDto[];
}

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
