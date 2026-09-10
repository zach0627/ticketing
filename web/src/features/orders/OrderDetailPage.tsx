import './ticket.css';
import { Link, useLocation, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ordersApi } from './api';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { BookingSteps } from '../../shared/ui/BookingSteps';
import { Icon } from '../../shared/ui/Icon';
import { formatPrice, formatTaipei } from '../../shared/utils/format';

export function OrderDetailPage() {
  const { orderId = '' } = useParams();
  const location = useLocation();
  const { data, isPending, error, refetch } = useQuery({
    queryKey: ['order', orderId],
    queryFn: ({ signal }) => ordersApi.get(orderId, signal),
  });
  if (isPending) return <Spinner />;
  if (error)
    return <ErrorMessage error={error} onRetry={() => void refetch()} />;
  return (
    <article className="order-detail">
      <Link className="back-link" to="/orders">
        ← 返回我的訂單
      </Link>
      {location.state?.purchased && <BookingSteps current={3} />}
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
              <li key={ticket.ticketCode} className="ticket">
                <div className="ticket__main">
                  <div className="ticket__top">
                    <span>
                      <Icon name="ticket" size={16} />
                      電子票券
                    </span>
                    <small>
                      0{i + 1} / 0{data.items.length}
                    </small>
                  </div>
                  <div className="ticket__seat">
                    <div>
                      <small>票區</small>
                      <strong>{ticket.sectionCode} 區</strong>
                    </div>
                    <div>
                      <small>排數</small>
                      <strong>{ticket.rowNumber} 排</strong>
                    </div>
                    <div>
                      <small>座號</small>
                      <strong>{ticket.seatNumber} 號</strong>
                    </div>
                  </div>
                  <div className="ticket__code">
                    <span>票券編號</span>
                    <code>{ticket.ticketCode}</code>
                  </div>
                  <p>票券無實際入場效力</p>
                </div>
                <div className="ticket__stub">
                  <Icon name="ticket" size={26} />
                  <span>TICKETING</span>
                  <strong>
                    {formatPrice(ticket.unitPrice, data.currency)}
                  </strong>
                  <small>模擬付款完成</small>
                </div>
              </li>
            ))}
          </ul>
        </section>
        <aside className="order-receipt panel">
          <h2>訂單資訊</h2>
          <dl>
            <div>
              <dt>訂單編號</dt>
              <dd>
                <code>{data.id}</code>
              </dd>
            </div>
            <div>
              <dt>訂購時間</dt>
              <dd>{formatTaipei(data.createdAtUtc)}</dd>
            </div>
            <div>
              <dt>票券數量</dt>
              <dd>{data.items.length} 張</dd>
            </div>
            <div>
              <dt>付款狀態</dt>
              <dd className="status status--onsale">模擬付款完成</dd>
            </div>
          </dl>
          <div className="selection-total">
            <span>合計</span>
            <strong>{formatPrice(data.totalAmount, data.currency)}</strong>
          </div>
          <Link className="cta cta--secondary" to="/">
            繼續探索活動
            <Icon name="arrow" size={16} />
          </Link>
        </aside>
      </div>
    </article>
  );
}
