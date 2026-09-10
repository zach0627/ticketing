import type { SelectionMode } from './SelectionMode';

export interface CreateHoldRequest {
  sectionId: number;
  quantity: number;
  selectionMode: SelectionMode;
  seatIds: number[];
}
