import type { SeatDto, SectionDto } from '../../catalog';

export function toggleSelection(
  current: number[],
  seat: SeatDto,
  seats: SeatDto[],
  limit: number,
): number[] {
  if (current.includes(seat.id)) return current.filter((id) => id !== seat.id);
  if (seat.status !== 'Available' || current.length >= limit) return current;
  const first = seats.find((s) => s.id === current[0]);
  if (first && first.sectionId !== seat.sectionId) return current;
  return [...current, seat.id];
}

export function selectedTotal(
  ids: number[],
  seats: SeatDto[],
  sections: SectionDto[],
): number {
  const prices = new Map(
    sections.map((section) => [section.id, section.price]),
  );
  return seats
    .filter((seat) => ids.includes(seat.id))
    .reduce((sum, seat) => sum + (prices.get(seat.sectionId) ?? 0), 0);
}

/** 登入前的選位草稿只供還原畫面；座位狀態與購票規則仍會重新驗證。 */
export function readSelectionDraft(
  value: unknown,
  performanceId: number,
): {
  sectionId: number;
  quantity: number;
  selectionMode: 'Manual' | 'Contiguous';
  seatIds: number[];
} | null {
  if (!value || typeof value !== 'object') return null;
  const draft = value as Record<string, unknown>;
  if (
    draft.performanceId !== performanceId ||
    !Number.isSafeInteger(draft.sectionId) ||
    !Number.isSafeInteger(draft.quantity) ||
    Number(draft.quantity) < 1 ||
    Number(draft.quantity) > 8 ||
    !['Manual', 'Contiguous'].includes(String(draft.selectionMode)) ||
    !Array.isArray(draft.seatIds) ||
    !draft.seatIds.every((id) => Number.isSafeInteger(id) && id > 0) ||
    new Set(draft.seatIds).size !== draft.seatIds.length
  )
    return null;
  if (
    draft.selectionMode === 'Manual' &&
    draft.seatIds.length !== draft.quantity
  )
    return null;
  if (draft.selectionMode === 'Contiguous' && draft.seatIds.length !== 0)
    return null;
  return {
    sectionId: Number(draft.sectionId),
    quantity: Number(draft.quantity),
    selectionMode: draft.selectionMode as 'Manual' | 'Contiguous',
    seatIds: draft.seatIds as number[],
  };
}
