import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { formatPrice, formatTaipei } from '../../../shared/utils/format';

import type { OrderDto } from '../model/OrderDto';

export function OrderReceipt({ data }: { data: OrderDto }) {
  return (
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
  );
}
