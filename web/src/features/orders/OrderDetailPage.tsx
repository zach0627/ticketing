import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ordersApi } from './api';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { formatPrice, formatTaipei } from '../../shared/utils/format';

export function OrderDetailPage() {
  const { orderId = '' } = useParams();

  const { data, isPending, error } = useQuery({
    queryKey: ['order', orderId],
    queryFn: ({ signal }) => ordersApi.get(orderId, signal),
  });

  if (isPending) return <Spinner />;
  if (error) return <ErrorMessage error={error} />;

  return (
    <article>
      <Link className="back-link" to="/orders">← 回到訂單列表</Link>

      <h1>{data.eventTitle}</h1>
      <p className="detail__performer">{formatTaipei(data.startsAtUtc)}</p>

      <ul className="ticket-list">
        {data.items.map((ticket) => (
          <li key={ticket.ticketCode} className="ticket">
            <div className="ticket__seat">
              {ticket.sectionCode} 區・{ticket.rowNumber} 排 {ticket.seatNumber} 號
            </div>
            <div className="ticket__code">{ticket.ticketCode}</div>
            <div className="ticket__price">{formatPrice(ticket.unitPrice, data.currency)}</div>
          </li>
        ))}
      </ul>

      <p className="hold__total">合計 {formatPrice(data.totalAmount, data.currency)}</p>
      <p className="page-header__note">
        下單時間 {formatTaipei(data.createdAtUtc)}・<strong>票券無實際入場效力</strong>。
      </p>
    </article>
  );
}
