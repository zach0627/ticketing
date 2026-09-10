import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { useLocation } from 'react-router';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useOrders } from '../src/features/orders/hooks/useOrders';
import { useAdminDashboard } from '../src/features/admin/hooks/useAdminDashboard';
import { useHoldDetails } from '../src/features/booking/hooks/useHoldDetails';
import { ordersApi } from '../src/features/orders/api/ordersApi';
import { adminApi } from '../src/features/admin/api/adminApi';
import { bookingApi } from '../src/features/booking/api/bookingApi';
import { catalogApi } from '../src/features/catalog/api/catalogApi';
import { testProviders } from './support/TestProviders';

vi.mock('../src/features/orders/api/ordersApi', () => ({
  ordersApi: { list: vi.fn() },
}));
vi.mock('../src/features/admin/api/adminApi', () => ({
  adminApi: {
    dashboard: vi.fn(),
    orders: vi.fn(),
    setSalesPaused: vi.fn(),
    reset: vi.fn(),
  },
}));
vi.mock('../src/features/booking/api/bookingApi', () => ({
  bookingApi: { getHold: vi.fn() },
}));
vi.mock('../src/features/catalog/api/catalogApi', () => ({
  catalogApi: { getSeatMap: vi.fn(), getEvent: vi.fn() },
}));
beforeEach(() => {
  vi.resetAllMocks();
  sessionStorage.clear();
});
afterEach(cleanup);

test('orders pagination changes the URL and fetches the corresponding server page', async () => {
  vi.mocked(ordersApi.list).mockResolvedValue({ items: [], total: 21 });
  const { result } = renderHook(
    () => ({ flow: useOrders(), location: useLocation() }),
    testProviders({ entry: '/orders' }),
  );
  await waitFor(() => expect(result.current.flow.query.isSuccess).toBe(true));
  act(() => result.current.flow.nextPage());
  await waitFor(() =>
    expect(ordersApi.list).toHaveBeenLastCalledWith(2, expect.any(AbortSignal)),
  );
  expect(result.current.location.search).toBe('?page=2');
  act(() => result.current.flow.previousPage());
  expect(result.current.flow.page).toBe(1);
});

test('admin filtering and pause mutation keep their API payload and refresh dashboard data', async () => {
  vi.mocked(adminApi.dashboard).mockResolvedValue({
    activeHolds: 0,
    soldSeats: 0,
    orders: 0,
    serverNowUtc: '2026-09-10T00:00:00Z',
    performances: [],
  });
  vi.mocked(adminApi.orders).mockResolvedValue({ items: [], total: 0 });
  vi.mocked(adminApi.setSalesPaused).mockResolvedValue({
    performanceId: 1,
    isSalesPaused: true,
  });
  const { result } = renderHook(
    useAdminDashboard,
    testProviders({ entry: '/admin' }),
  );
  await waitFor(() => expect(result.current.dashboard.isSuccess).toBe(true));
  act(() => result.current.setPerformanceId(1));
  await waitFor(() =>
    expect(adminApi.orders).toHaveBeenLastCalledWith(
      1,
      expect.any(AbortSignal),
    ),
  );
  await act(() =>
    result.current.togglePause.mutateAsync({ id: 1, paused: true }),
  );
  expect(adminApi.setSalesPaused).toHaveBeenCalledExactlyOnceWith(
    1,
    true,
    expect.any(String),
  );
  await waitFor(() =>
    expect(result.current.message).toBe('場次 1 已暫停售票。'),
  );
  expect(adminApi.dashboard).toHaveBeenCalledTimes(2);
});

test('hold expiration refetches once and dependent reads reuse the seat-map and event contracts', async () => {
  vi.mocked(bookingApi.getHold).mockResolvedValue({
    id: 'hold-1',
    performanceId: 1,
    status: 'Active',
    orderId: null,
    serverNowUtc: '2026-09-10T00:00:00Z',
    expiresAtUtc: '2026-09-10T00:00:00Z',
    currency: 'TWD',
    totalAmount: 0,
    items: [],
  });
  vi.mocked(catalogApi.getSeatMap).mockResolvedValue({
    eventCode: 'C01',
  } as never);
  vi.mocked(catalogApi.getEvent).mockResolvedValue({ code: 'C01' } as never);
  const { result, rerender } = renderHook(
    useHoldDetails,
    testProviders({ entry: '/holds/hold-1', path: '/holds/:holdId' }),
  );
  await waitFor(() => expect(result.current.event?.code).toBe('C01'));
  expect(bookingApi.getHold).toHaveBeenCalledTimes(2);
  expect(catalogApi.getSeatMap).toHaveBeenCalledWith(
    1,
    expect.any(AbortSignal),
  );
  expect(catalogApi.getEvent).toHaveBeenCalledWith(
    'C01',
    expect.any(AbortSignal),
  );
  expect(result.current.remaining).toBe(0);
  rerender();
  expect(bookingApi.getHold).toHaveBeenCalledTimes(2);
});
