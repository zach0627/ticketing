import { Icon } from '../../../shared/ui/Icon';
import { formatPrice } from '../../../shared/utils/format';

import type { OrderItemDto } from '../model/OrderItemDto';

export function ElectronicTicket({
  ticket,
  index,
  quantity,
  currency,
}: {
  ticket: OrderItemDto;
  index: number;
  quantity: number;
  currency: string;
}) {
  return (
    <li className="ticket">
      <div className="ticket__main">
        <div className="ticket__top">
          <span>
            <Icon name="ticket" size={16} />
            電子票券
          </span>
          <small>
            0{index + 1} / 0{quantity}
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
        <strong>{formatPrice(ticket.unitPrice, currency)}</strong>
        <small>模擬付款完成</small>
      </div>
    </li>
  );
}
