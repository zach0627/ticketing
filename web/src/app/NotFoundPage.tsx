import './styles/guide.css';
import { Link } from 'react-router';
import { Icon } from '../shared/ui/Icon';

export function NotFoundPage() {
  return (
    <section className="not-found">
      <Icon name="ticket" size={48} />
      <h1>這一頁暫時缺席了</h1>
      <p>連結可能已失效，回到活動列表繼續探索吧。</p>
      <Link to="/" className="cta">
        回到活動列表
        <Icon name="arrow" size={18} />
      </Link>
    </section>
  );
}
