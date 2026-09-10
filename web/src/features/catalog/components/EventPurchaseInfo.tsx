import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { formatPrice, formatTaipei } from '../../../shared/utils/format';
import { describeSalesStatus } from '../model/describeSalesStatus';

import type { EventDetailDto } from '../model/EventDetailDto';

export function EventPurchaseInfo({
  data,
  onSale,
  minPrice,
  purchaseLink,
}: {
  data: EventDetailDto;
  onSale: boolean;
  minPrice: number;
  purchaseLink: string;
}) {
  const { performance } = data;
  return (
    <aside className="detail-sidebar">
      <h2>購票資訊</h2>
      <p>
        選好喜歡的票區，
        <br />
        前往座位圖開始選位。
      </p>
      <div className="detail-sidebar__price">
        {formatPrice(minPrice, data.currency)} <small>起</small>
      </div>
      {onSale ? (
        <Link className="cta" to={purchaseLink}>
          選擇座位
          <Icon name="arrow" size={17} />
        </Link>
      ) : (
        <p className="notice">
          {performance.salesStatus === 'NotYetOnSale'
            ? `${formatTaipei(performance.salesOpensAtUtc)} 開賣`
            : describeSalesStatus(performance.salesStatus)}
        </p>
      )}
      <small>
        <Icon name="shield" size={14} />
        每人限購 {data.maxTicketsPerBuyer} 張
      </small>
    </aside>
  );
}
