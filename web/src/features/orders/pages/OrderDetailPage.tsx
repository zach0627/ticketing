import '../styles/ticket.css';
import { Link } from 'react-router';
import { BookingSteps } from '../../../shared/ui/BookingSteps';
import { Icon } from '../../../shared/ui/Icon';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { formatTaipei } from '../../../shared/utils/format';
import { ElectronicTicket } from '../components/ElectronicTicket';
import { OrderReceipt } from '../components/OrderReceipt';
import { useOrderDetail } from '../hooks/useOrderDetail';

export function OrderDetailPage() {
  const { query, purchased } = useOrderDetail();
  const { data, isPending, error, refetch } = query;
  if (isPending) return <Spinner />;
  if (error)
    return <ErrorMessage error={error} onRetry={() => void refetch()} />;
  return (
    <article className="order-detail">
      <Link className="back-link" to="/orders">
        ← 返回我的訂單
      </Link>
      {purchased && <BookingSteps current={3} />}
      <header className="order-success">
        <span className="order-success__icon">
          <Icon name="check" size={28} />
        </span>
        <h1>購票完成</h1>
        <p>
          你的 {data.items.length} 張票券已準備好，可隨時在「我的訂單」查看。
        </p>
      </header>
      <div className="order-detail__layout">
        <section>
          <div className="ticket-event-heading">
            <h2>{data.eventTitle}</h2>
            <p>
              <Icon name="calendar" size={16} />
              {formatTaipei(data.startsAtUtc)}
            </p>
          </div>
          <ul className="ticket-list">
            {data.items.map((ticket, i) => (
              <ElectronicTicket
                key={ticket.ticketCode}
                ticket={ticket}
                index={i}
                quantity={data.items.length}
                currency={data.currency}
              />
            ))}
          </ul>
        </section>
        <OrderReceipt data={data} />
      </div>
    </article>
  );
}
