import type { HoldItemDto } from './HoldItemDto';
import type { HoldStatus } from './HoldStatus';

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
