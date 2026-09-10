import { apiGet, apiPost, withRetry } from '../../../shared/api/http';
import type { OrderDto } from '../../orders';
import type { CreateHoldRequest } from '../model/CreateHoldRequest';
import type { HoldDto } from '../model/HoldDto';

/**
 * 三個寫入端點都要帶 `Idempotency-Key`。
 * key 由呼叫端決定並負責保存——**重送時一定沿用同一個**，
 * 換 key 就等於「這是另一次購買」（設計文件 06 第 7 節）。
 */
export const bookingApi = {
  createHold: (performanceId: number, body: CreateHoldRequest, key: string) =>
    withRetry(() =>
      apiPost<HoldDto>(`/performances/${performanceId}/holds`, body, {
        headers: { 'Idempotency-Key': key },
      }),
    ),

  checkout: (holdId: string, outcome: 'Succeeded' | 'Failed', key: string) =>
    withRetry(() =>
      apiPost<OrderDto>(
        `/holds/${holdId}/checkout`,
        { outcome },
        {
          headers: { 'Idempotency-Key': key },
        },
      ),
    ),

  /** 取消不需要 key：它的資料效果本來就是冪等的。 */
  cancel: (holdId: string) => apiPost<HoldDto>(`/holds/${holdId}/cancel`, {}),

  getHold: (holdId: string, signal?: AbortSignal) =>
    apiGet<HoldDto>(`/holds/${holdId}`, signal),

  myHolds: (performanceId: number, signal?: AbortSignal) =>
    apiGet<{ items: HoldDto[] }>(
      `/me/holds?performanceId=${performanceId}`,
      signal,
    ),
};
