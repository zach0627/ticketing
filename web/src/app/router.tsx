import { createBrowserRouter } from 'react-router';
import { AppLayout } from './AppLayout';
import { HomePage } from '../features/catalog/HomePage';
import { EventDetailPage } from '../features/catalog/EventDetailPage';
import { SeatSelectionPage } from '../features/booking/SeatSelectionPage';
import { HoldPage } from '../features/booking/HoldPage';
import { AccountPage } from '../features/auth/AccountPage';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterPage } from '../features/auth/RegisterPage';
import { RequireAuth } from '../features/auth/RequireAuth';
import { OrdersPage } from '../features/orders/OrdersPage';
import { OrderDetailPage } from '../features/orders/OrderDetailPage';
import { AdminPage } from '../features/admin/AdminPage';
import { RequireAdmin } from '../features/admin/RequireAdmin';

/** 路由表對應設計文件 13 第 4 節。 */
export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      // 公開
      { path: '/', element: <HomePage /> },
      { path: '/events/:code', element: <EventDetailPage /> },
      // 座位圖本身公開；按下「保留」才需要登入（會記住路徑再導去登入頁）
      { path: '/performances/:performanceId/seats', element: <SeatSelectionPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },

      // 需要登入
      { path: '/account', element: <RequireAuth><AccountPage /></RequireAuth> },
      { path: '/holds/:holdId', element: <RequireAuth><HoldPage /></RequireAuth> },
      { path: '/orders', element: <RequireAuth><OrdersPage /></RequireAuth> },
      { path: '/orders/:orderId', element: <RequireAuth><OrderDetailPage /></RequireAuth> },

      // 只有管理者。前端擋只是 UX，真正的把關是後端的 [Authorize(Roles = "Admin")]
      { path: '/admin', element: <RequireAdmin><AdminPage /></RequireAdmin> },
    ],
  },
]);
