import '../styles/orders.css';
import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { Empty, ErrorMessage, Spinner } from '../../../shared/ui/States';
import { OrderCard } from '../components/OrderCard';
import { OrdersPagination } from '../components/OrdersPagination';
import { useOrders } from '../hooks/useOrders';

export function OrdersPage() {
  const { query, page, previousPage, nextPage } = useOrders();
  const { data, isPending, error, refetch } = query;
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
              <OrderCard order={order} />
            </li>
          ))}
        </ul>
      )}
      <OrdersPagination
        total={data.total}
        page={page}
        previousPage={previousPage}
        nextPage={nextPage}
      />
      <p className="notice">
        <Icon name="info" size={17} />
        <span>付款為模擬流程，票券無實際入場效力。</span>
      </p>
    </section>
  );
}
