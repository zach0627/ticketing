import './orders.css';
import { Link, useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ordersApi } from './api';
import { Empty, ErrorMessage, Spinner } from '../../shared/ui/States';
import { Icon } from '../../shared/ui/Icon';
import {
  formatPrice,
  formatTaipei,
  formatTaipeiDate,
} from '../../shared/utils/format';

export function OrdersPage() {
  const [params, setParams] = useSearchParams();
  const parsedPage = Number(params.get('page') ?? 1);
  const page =
    Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const { data, isPending, error, refetch } = useQuery({
    queryKey: ['orders', page],
    queryFn: ({ signal }) => ordersApi.list(page, signal),
  });
  if (isPending) return <Spinner />;
  if (error)
    return <ErrorMessage error={error} onRetry={() => void refetch()} />;
  return (
    <section className="orders-page">
      <header className="page-heading">
        <h1>我的訂單</h1>
        <p>查看已完成的訂單與票券。</p>
      </header>
      <div className="orders-toolbar">
        <span>
          <Icon name="ticket" size={18} />
          已完成訂單 <b>{data.total}</b>
        </span>
        <Link to="/">
          繼續探索活動
          <Icon name="arrow" size={16} />
        </Link>
      </div>
      {data.items.length === 0 ? (
        <Empty>
          <Icon name="ticket" size={42} />
          <strong>你的第一場現場，正在等你。</strong>
          <span>完成購票後，訂單與票券就會出現在這裡。</span>
          <Link className="cta" to="/">
            探索活動
            <Icon name="arrow" size={17} />
          </Link>
        </Empty>
      ) : (
        <ul className="order-list">
          {data.items.map((order) => (
            <li key={order.id}>
              <Link className="order-card" to={`/orders/${order.id}`}>
                <div className="order-card__icon">
                  <Icon name="ticket" size={30} />
                </div>
                <div className="order-card__content">
                  <span className="status status--onsale">購票完成</span>
                  <h2>{order.eventTitle}</h2>
                  <p>
                    <Icon name="calendar" size={15} />
                    {formatTaipeiDate(order.startsAtUtc)}
                    <span>{order.quantity} 張票券</span>
                  </p>
                  <small>訂購於 {formatTaipei(order.createdAtUtc)}</small>
                </div>
                <div className="order-card__action">
                  <strong>
                    {formatPrice(order.totalAmount, order.currency)}
                  </strong>
                  <span>
                    查看票券
                    <Icon name="arrow" size={16} />
                  </span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {data.total > 10 && (
        <nav className="pagination" aria-label="訂單分頁">
          <button
            className="cta cta--secondary"
            disabled={page <= 1}
            onClick={() => setParams({ page: String(page - 1) })}
          >
            上一頁
          </button>
          <span>
            第 {page} / {Math.ceil(data.total / 10)} 頁
          </span>
          <button
            className="cta cta--secondary"
            disabled={page * 10 >= data.total}
            onClick={() => setParams({ page: String(page + 1) })}
          >
            下一頁
          </button>
        </nav>
      )}
      <p className="notice">
        <Icon name="info" size={17} />
        <span>付款為模擬流程，票券無實際入場效力。</span>
      </p>
    </section>
  );
}
