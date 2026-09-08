import { Outlet } from 'react-router';
import { SiteHeader } from './SiteHeader';

/** 版面外框：所有頁面共用的頁首，內容由巢狀路由填進 Outlet。 */
export function AppLayout() {
  return (
    <>
      <SiteHeader />
      <main>
        <Outlet />
      </main>
    </>
  );
}
