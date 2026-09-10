import './auth.css';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { EventImage } from '../../shared/ui/EventImage';
import { Icon } from '../../shared/ui/Icon';

export function AuthShell({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="auth-layout">
      <aside className="auth-story">
        <div className="auth-story__copy">
          <h2>
            探索精彩活動，
            <br />
            從這裡開始。
          </h2>
          <p>登入會員即可選位購票、查詢訂單與票券。</p>
        </div>
        <EventImage
          src="/assets/events/C10-v2.webp"
          alt="ORBIT FOUR 活動主視覺"
        />
        <div className="auth-story__caption">
          <span>ORBIT FOUR</span>
          <span>平行星球 · LIVE TOUR</span>
        </div>
      </aside>
      <section className="auth">
        <Link className="back-link" to="/">
          <Icon
            name="chevron"
            size={13}
            style={{ transform: 'rotate(180deg)' }}
          />
          回到活動列表
        </Link>
        <h1>{title}</h1>
        <p className="auth__subtitle">{subtitle}</p>
        {children}
        <p className="auth__disclaimer">活動為虛構內容，票券無實際入場效力。</p>
      </section>
    </div>
  );
}
