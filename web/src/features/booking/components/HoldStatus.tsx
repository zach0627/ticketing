import { Link } from 'react-router';
import { BookingSteps } from '../../../shared/ui/BookingSteps';
import { Icon } from '../../../shared/ui/Icon';

import type { HoldDto } from '../model/HoldDto';

export function HoldStatus({ data }: { data: HoldDto }) {
  const completed = data.status === 'Completed' && data.orderId;
  return (
    <section className="hold">
      <BookingSteps current={completed ? 3 : 2} />
      <div className="hold-status-panel">
        <Icon name={completed ? 'check' : 'clock'} size={40} />
        <h1>
          {completed
            ? '購票已完成，票券準備好了。'
            : data.status === 'Expired'
              ? '這次座位保留已到期'
              : '已取消座位保留'}
        </h1>
        <p>
          {completed
            ? '前往訂單查看你的活動與座位明細。'
            : '座位已重新開放，可以回到座位圖再次選擇。'}
        </p>
        <Link
          className="cta"
          to={
            completed
              ? `/orders/${data.orderId}`
              : `/performances/${data.performanceId}/seats`
          }
        >
          {completed ? '查看票券' : '重新選位'}
          <Icon name="arrow" size={17} />
        </Link>
      </div>
    </section>
  );
}
