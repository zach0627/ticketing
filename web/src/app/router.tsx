import { createBrowserRouter } from 'react-router';
import { AppLayout } from './AppLayout';
import { HomePage } from '../features/catalog/HomePage';
import { EventDetailPage } from '../features/catalog/EventDetailPage';
import { SeatMapPage } from '../features/catalog/SeatMapPage';
import { AccountPage } from '../features/auth/AccountPage';
import { LoginPage } from '../features/auth/LoginPage';
import { RegisterPage } from '../features/auth/RegisterPage';
import { RequireAuth } from '../features/auth/RequireAuth';

/** 路由表對應設計文件 13 第 4 節。保留、訂單、後台在階段 6／7 加入。 */
export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/events/:code', element: <EventDetailPage /> },
      { path: '/performances/:performanceId/seats', element: <SeatMapPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
      {
        path: '/account',
        element: (
          <RequireAuth>
            <AccountPage />
          </RequireAuth>
        ),
      },
    ],
  },
]);
