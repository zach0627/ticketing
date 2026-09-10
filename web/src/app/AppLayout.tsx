import { Outlet, ScrollRestoration, useLocation } from 'react-router';
import { useAuth } from '../features/auth';
import { SiteFooter } from './components/SiteFooter';
import { SiteHeader } from './components/SiteHeader';

/** 版面外框：所有頁面共用的頁首，內容由巢狀路由填進 Outlet。 */
export function AppLayout() {
  const { pathname } = useLocation();
  const { user } = useAuth();
  return (
    <>
      <a className="skip-link" href="#main-content">
        跳至主要內容
      </a>
      <SiteHeader />
      <main
        id="main-content"
        className={
          pathname === '/'
            ? 'main-content main-content--home'
            : 'container main-content'
        }
        tabIndex={-1}
      >
        <Outlet key={`${pathname}:${user?.id ?? 'guest'}`} />
      </main>
      <SiteFooter />
      <ScrollRestoration />
    </>
  );
}
