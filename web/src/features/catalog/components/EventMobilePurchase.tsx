import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { formatPrice } from '../../../shared/utils/format';
import { describeSalesStatus } from '../model/describeSalesStatus';

import type { EventDetailDto } from '../model/EventDetailDto';

export function EventMobilePurchase({
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
    <div className="detail-mobile-purchase">
      <div>
        <span>{describeSalesStatus(performance.salesStatus)}</span>
        <strong>
          {formatPrice(minPrice, data.currency)} <small>起</small>
        </strong>
      </div>
      {onSale ? (
        <Link className="cta" to={purchaseLink}>
          立即購票
          <Icon name="arrow" size={17} />
        </Link>
      ) : (
        <span className="cta cta--disabled">
          {describeSalesStatus(performance.salesStatus)}
        </span>
      )}
    </div>
  );
}
