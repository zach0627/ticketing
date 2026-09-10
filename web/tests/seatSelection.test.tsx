import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useSeatSelection } from '../src/features/booking/hooks/useSeatSelection';
import { testProviders } from './support/TestProviders';
import type { SeatMapDto } from '../src/features/catalog';

const state = vi.hoisted(() => ({
  data: undefined as SeatMapDto | undefined,
  uncertain: false,
  reserve: vi.fn(),
  setError: vi.fn(),
}));
vi.mock('../src/features/booking/hooks/useSeatMap', () => ({
  useSeatMap: () => ({
    data: state.data,
    refetch: vi.fn(),
    isPending: !state.data,
    error: null,
  }),
}));
vi.mock('../src/features/booking/hooks/useReservation', () => ({
  useReservation: () => ({
    user: { id: 'buyer-a' },
    error: null,
    setError: state.setError,
    isSubmitting: false,
    uncertain: state.uncertain,
    reserve: state.reserve,
  }),
}));
const seatMap: SeatMapDto = {
  performanceId: 1,
  eventCode: 'C01',
  eventTitle: '巡迴演唱會',
  category: 'Concert',
  maxTicketsPerBuyer: 2,
  allowsContiguousAllocation: true,
  salesStatus: 'OnSale',
  serverNowUtc: '2026-09-10T00:00:00Z',
  sections: [
    {
      id: 10,
      code: 'A',
      name: 'A 區',
      price: 3000,
      rowCount: 1,
      seatsPerRow: 3,
    },
    {
      id: 20,
      code: 'B',
      name: 'B 區',
      price: 2000,
      rowCount: 1,
      seatsPerRow: 2,
    },
  ],
  seats: [1, 2, 3].map((id) => ({
    id,
    sectionId: 10,
    rowNumber: 1,
    seatNumber: id,
    status: 'Available',
  })),
};
beforeEach(() => {
  vi.clearAllMocks();
  state.data = structuredClone(seatMap);
  state.uncertain = false;
});
afterEach(cleanup);

test('selection controller preserves limits, clears seats when changing mode, and sends the same allocation payload', () => {
  const { result } = renderHook(
    useSeatSelection,
    testProviders({ path: '/performances/:performanceId/seats' }),
  );
  act(() => result.current.toggleSeat(seatMap.seats[0]));
  act(() => result.current.toggleSeat(seatMap.seats[1]));
  act(() => result.current.toggleSeat(seatMap.seats[2]));
  expect(result.current.selected).toEqual([1, 2]);
  expect(result.current.total).toBe(6000);
  expect(state.setError).toHaveBeenLastCalledWith(
    '本場每人最多 2 張，可先取消已選座位再更換。',
  );
  act(() => result.current.submit());
  expect(state.reserve).toHaveBeenLastCalledWith({
    sectionId: 10,
    quantity: 2,
    selectionMode: 'Manual',
    seatIds: [1, 2],
  });
  act(() => result.current.changeMode(true));
  expect(result.current.selected).toEqual([]);
  act(() => result.current.changeSection(20));
  expect(result.current.total).toBe(4000);
  act(() => result.current.submit());
  expect(state.reserve).toHaveBeenLastCalledWith({
    sectionId: 20,
    quantity: 2,
    selectionMode: 'Contiguous',
    seatIds: [],
  });
});

test('login draft survives loading, stale seats block new holds, and uncertain outcomes can still be recovered', () => {
  state.data = undefined;
  const { result, rerender } = renderHook(
    useSeatSelection,
    testProviders({
      path: '/performances/:performanceId/seats',
      state: {
        seatDraft: {
          performanceId: 1,
          sectionId: 10,
          quantity: 1,
          selectionMode: 'Manual',
          seatIds: [1],
        },
      },
    }),
  );
  expect(result.current.selected).toEqual([1]);
  act(() => result.current.submit());
  expect(state.reserve).not.toHaveBeenCalled();
  state.data = {
    ...seatMap,
    seats: seatMap.seats.map((seat) => ({ ...seat, status: 'Held' })),
  };
  rerender();
  expect(result.current.staleSelection).toBe(true);
  act(() => result.current.submit());
  expect(state.reserve).not.toHaveBeenCalled();
  state.uncertain = true;
  rerender();
  expect(result.current.locked).toBe(true);
  act(() => result.current.submit());
  expect(state.reserve).toHaveBeenCalledOnce();
});
