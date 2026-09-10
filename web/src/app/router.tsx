import { createBrowserRouter, Outlet } from 'react-router';
import { AppLayout } from './AppLayout';
import { RequireAuth } from '../features/auth/RequireAuth';
import { RequireAdmin } from '../features/admin/RequireAdmin';
import { Spinner } from '../shared/ui/States';

/** 路由依功能載入；授權容器只決定呈現，資源存取仍由 API 驗證。 */
export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    HydrateFallback: Spinner,
    children: [
      {
        path: '/',
        lazy: async () => ({
          Component: (await import('../features/catalog/HomePage')).HomePage,
        }),
      },
      {
        path: '/guide',
        lazy: async () => ({
          Component: (await import('./GuidePage')).GuidePage,
        }),
      },
      {
        path: '/events/:code',
        lazy: async () => ({
          Component: (await import('../features/catalog/EventDetailPage'))
            .EventDetailPage,
        }),
      },
      // 座位圖公開；送出保留前登入，並接續選位草稿。
      {
        path: '/performances/:performanceId/seats',
        lazy: async () => ({
          Component: (await import('../features/booking/SeatSelectionPage'))
            .SeatSelectionPage,
        }),
      },
      {
        path: '/login',
        lazy: async () => ({
          Component: (await import('../features/auth/LoginPage')).LoginPage,
        }),
      },
      {
        path: '/register',
        lazy: async () => ({
          Component: (await import('../features/auth/RegisterPage'))
            .RegisterPage,
        }),
      },
      {
        element: (
          <RequireAuth>
            <Outlet />
          </RequireAuth>
        ),
        children: [
          {
            path: '/account',
            lazy: async () => ({
              Component: (await import('../features/auth/AccountPage'))
                .AccountPage,
            }),
          },
          {
            path: '/holds/:holdId',
            lazy: async () => ({
              Component: (await import('../features/booking/HoldPage'))
                .HoldPage,
            }),
          },
          {
            path: '/orders',
            lazy: async () => ({
              Component: (await import('../features/orders/OrdersPage'))
                .OrdersPage,
            }),
          },
          {
            path: '/orders/:orderId',
            lazy: async () => ({
              Component: (await import('../features/orders/OrderDetailPage'))
                .OrderDetailPage,
            }),
          },
        ],
      },
      {
        element: (
          <RequireAdmin>
            <Outlet />
          </RequireAdmin>
        ),
        children: [
          {
            path: '/admin',
            lazy: async () => ({
              Component: (await import('../features/admin/AdminPage'))
                .AdminPage,
            }),
          },
        ],
      },
      {
        path: '*',
        lazy: async () => ({
          Component: (await import('./NotFoundPage')).NotFoundPage,
        }),
      },
    ],
  },
]);
