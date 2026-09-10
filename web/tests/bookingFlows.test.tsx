import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { useLocation } from 'react-router';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useReservation } from '../src/features/booking/hooks/useReservation';
import { useCheckout } from '../src/features/booking/hooks/useCheckout';
import { bookingApi } from '../src/features/booking/api/bookingApi';
import { pendingOperations } from '../src/features/booking/api/pendingOperations';
import { ApiError } from '../src/shared/api/http';
import { testProviders } from './support/TestProviders';

vi.mock('../src/features/booking/api/bookingApi', () => ({
  bookingApi: {
    myHolds: vi.fn(),
    createHold: vi.fn(),
    checkout: vi.fn(),
    cancel: vi.fn(),
  },
}));
const payload = {
  sectionId: 10,
  quantity: 1,
  selectionMode: 'Manual' as const,
  seatIds: [101],
};
const refresh = vi.fn(async () => undefined);
beforeEach(() => {
  vi.resetAllMocks();
  sessionStorage.clear();
  vi.mocked(bookingApi.myHolds).mockResolvedValue({ items: [] });
});
afterEach(cleanup);

test('guest reservation returns to login with the same performance and seat draft', async () => {
  const { result } = renderHook(
    () => ({
      flow: useReservation(1, refresh, vi.fn()),
      location: useLocation(),
    }),
    testProviders({ user: null }),
  );
  await act(() => result.current.flow.reserve(payload));
  expect(bookingApi.createHold).not.toHaveBeenCalled();
  expect(result.current.location.pathname).toBe('/login');
  expect(result.current.location.state).toEqual({
    from: '/performances/1/seats',
    seatDraft: { performanceId: 1, ...payload },
  });
});

test('uncertain reservation survives remount and retries its original key and seats', async () => {
  vi.mocked(bookingApi.createHold).mockRejectedValueOnce(
    new TypeError('connection lost'),
  );
  const rejected = vi.fn();
  const first = renderHook(
    () => useReservation(1, refresh, rejected),
    testProviders(),
  );
  await act(() => first.result.current.reserve(payload));
  const key = vi.mocked(bookingApi.createHold).mock.calls[0][2];
  expect(first.result.current.uncertain).toBe(true);
  expect(rejected).not.toHaveBeenCalled();
  first.unmount();
  vi.mocked(bookingApi.createHold).mockResolvedValueOnce({
    id: 'hold-1',
  } as never);
  const second = renderHook(
    () => ({
      flow: useReservation(1, refresh, rejected),
      location: useLocation(),
    }),
    testProviders(),
  );
  expect(second.result.current.flow.uncertain).toBe(true);
  await act(() =>
    second.result.current.flow.reserve({ ...payload, seatIds: [102] }),
  );
  expect(bookingApi.createHold).toHaveBeenLastCalledWith(1, payload, key);
  expect(second.result.current.location.pathname).toBe('/holds/hold-1');
  expect(pendingOperations.read('buyer-a', 'hold', '1')).toBeNull();
});

test('definitive reservation rejection clears saved request and rejects the selection', async () => {
  vi.mocked(bookingApi.createHold).mockRejectedValue(
    new ApiError(409, 'SeatUnavailable', '座位無法保留'),
  );
  const rejected = vi.fn();
  const { result } = renderHook(
    () => useReservation(1, refresh, rejected),
    testProviders(),
  );
  await act(() => result.current.reserve(payload));
  expect(result.current.uncertain).toBe(false);
  expect(rejected).toHaveBeenCalledOnce();
  expect(pendingOperations.read('buyer-a', 'hold', '1')).toBeNull();
});

test('existing active hold redirects to recovery without creating another hold', async () => {
  vi.mocked(bookingApi.myHolds).mockResolvedValue({
    items: [{ id: 'existing' }],
  } as never);
  const { result } = renderHook(
    () => ({
      flow: useReservation(1, refresh, vi.fn()),
      location: useLocation(),
    }),
    testProviders(),
  );
  await waitFor(() =>
    expect(result.current.location.pathname).toBe('/holds/existing'),
  );
  expect(bookingApi.createHold).not.toHaveBeenCalled();
});

test('checkout blocks concurrent submission and cancellation, then reuses uncertain payment outcome', async () => {
  let reject!: (error: unknown) => void;
  vi.mocked(bookingApi.checkout).mockImplementationOnce(
    () =>
      new Promise((_, fail) => {
        reject = fail;
      }),
  );
  const first = renderHook(
    () => useCheckout('hold-1', 1, refresh),
    testProviders(),
  );
  let inFlight!: Promise<void>;
  act(() => {
    inFlight = first.result.current.pay('Succeeded');
    void first.result.current.pay('Failed');
    void first.result.current.cancel();
  });
  expect(bookingApi.checkout).toHaveBeenCalledTimes(1);
  expect(bookingApi.cancel).not.toHaveBeenCalled();
  await act(async () => {
    reject(new ApiError(503, 'Unavailable', '稍後重試'));
    await inFlight;
  });
  const key = vi.mocked(bookingApi.checkout).mock.calls[0][2];
  await act(() => first.result.current.cancel());
  expect(bookingApi.cancel).not.toHaveBeenCalled();
  first.unmount();
  vi.mocked(bookingApi.checkout).mockResolvedValueOnce({
    id: 'order-1',
  } as never);
  const second = renderHook(
    () => ({
      flow: useCheckout('hold-1', 1, refresh),
      location: useLocation(),
    }),
    testProviders(),
  );
  expect(second.result.current.flow.uncertain).toBe(true);
  await act(() => second.result.current.flow.pay('Failed'));
  expect(bookingApi.checkout).toHaveBeenLastCalledWith(
    'hold-1',
    'Succeeded',
    key,
  );
  expect(second.result.current.location.pathname).toBe('/orders/order-1');
  expect(second.result.current.location.state).toEqual({ purchased: true });
  expect(pendingOperations.read('buyer-a', 'checkout', 'hold-1')).toBeNull();
});

test('definitive payment failure permits a new operation with a fresh key', async () => {
  vi.mocked(bookingApi.checkout).mockRejectedValueOnce(
    new ApiError(409, 'PaymentFailed', '付款失敗'),
  );
  const { result } = renderHook(
    () => useCheckout('hold-1', 1, refresh),
    testProviders(),
  );
  await act(() => result.current.pay('Failed'));
  const key = vi.mocked(bookingApi.checkout).mock.calls[0][2];
  expect(result.current.uncertain).toBe(false);
  expect(pendingOperations.read('buyer-a', 'checkout', 'hold-1')).toBeNull();
  vi.mocked(bookingApi.checkout).mockResolvedValueOnce({
    id: 'order-1',
  } as never);
  await act(() => result.current.pay('Succeeded'));
  expect(vi.mocked(bookingApi.checkout).mock.calls[1][2]).not.toBe(key);
});
