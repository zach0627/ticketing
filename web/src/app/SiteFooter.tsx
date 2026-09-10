import { Link } from 'react-router';
import { Icon } from '../shared/ui/Icon';

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="container site-footer__main">
        <div>
          <Link to="/" className="brand">
            <Icon name="ticket" size={28} />
            <span>
              線上購票系統<small>ticketing</small>
            </span>
          </Link>
          <p>把喜歡的現場，放進生活裡。</p>
        </div>
        <nav aria-label="頁尾導覽">
          <Link to="/#events">探索活動</Link>
          <Link to="/orders">我的訂單</Link>
          <Link to="/guide">購票指南</Link>
        </nav>
      </div>
      <div className="container site-footer__bottom">
        <span>© {new Date().getFullYear()} ticketing 線上購票系統</span>
        <span>活動皆為虛構・付款為模擬流程・票券無實際入場效力</span>
      </div>
    </footer>
  );
}
