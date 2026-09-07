import { createBrowserRouter } from 'react-router';
import { HomePage } from '../features/catalog/HomePage';
import { EventDetailPage } from '../features/catalog/EventDetailPage';
import { SeatMapPage } from '../features/catalog/SeatMapPage';

/** 路由表對應設計文件 13 第 4 節。登入、保留、訂單、後台在後續階段加入。 */
export const router = createBrowserRouter([
  { path: '/', element: <HomePage /> },
  { path: '/events/:code', element: <EventDetailPage /> },
  { path: '/performances/:performanceId/seats', element: <SeatMapPage /> },
]);
