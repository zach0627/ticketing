import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import {
  formatPrice,
  formatTaipei,
  formatTaipeiDate,
} from '../../../shared/utils/format';

import type { OrderSummaryDto } from '../model/OrderSummaryDto';

export function OrderCard({ order }: { order: OrderSummaryDto }) {
  return (
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
        <strong>{formatPrice(order.totalAmount, order.currency)}</strong>
        <span>
          查看票券
          <Icon name="arrow" size={16} />
        </span>
      </div>
    </Link>
  );
}
