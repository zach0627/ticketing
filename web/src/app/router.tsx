import { Outlet, createBrowserRouter } from 'react-router';
import { RequireAdmin } from '../features/admin';
import { RequireAuth } from '../features/auth';
import { Spinner } from '../shared/ui/States';
import { AppLayout } from './AppLayout';

/** 路由依功能載入；授權容器只決定呈現，資源存取仍由 API 驗證。 */
export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    HydrateFallback: Spinner,
    children: [
      {
        path: '/',
        lazy: async () => ({
          Component: (await import('../features/catalog/pages/HomePage'))
            .HomePage,
        }),
      },
      {
        path: '/guide',
        lazy: async () => ({
          Component: (await import('./pages/GuidePage')).GuidePage,
        }),
      },
      {
        path: '/events/:code',
        lazy: async () => ({
          Component: (await import('../features/catalog/pages/EventDetailPage'))
            .EventDetailPage,
        }),
      },
      // 座位圖公開；送出保留前登入，並接續選位草稿。
      {
        path: '/performances/:performanceId/seats',
        lazy: async () => ({
          Component: (
            await import('../features/booking/pages/SeatSelectionPage')
          ).SeatSelectionPage,
        }),
      },
      {
        path: '/login',
        lazy: async () => ({
          Component: (await import('../features/auth/pages/LoginPage'))
            .LoginPage,
        }),
      },
      {
        path: '/register',
        lazy: async () => ({
          Component: (await import('../features/auth/pages/RegisterPage'))
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
              Component: (await import('../features/auth/pages/AccountPage'))
                .AccountPage,
            }),
          },
          {
            path: '/holds/:holdId',
            lazy: async () => ({
              Component: (await import('../features/booking/pages/HoldPage'))
                .HoldPage,
            }),
          },
          {
            path: '/orders',
            lazy: async () => ({
              Component: (await import('../features/orders/pages/OrdersPage'))
                .OrdersPage,
            }),
          },
          {
            path: '/orders/:orderId',
            lazy: async () => ({
              Component: (
                await import('../features/orders/pages/OrderDetailPage')
              ).OrderDetailPage,
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
              Component: (await import('../features/admin/pages/AdminPage'))
                .AdminPage,
            }),
          },
        ],
      },
      {
        path: '*',
        lazy: async () => ({
          Component: (await import('./pages/NotFoundPage')).NotFoundPage,
        }),
      },
    ],
  },
]);
