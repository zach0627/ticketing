import { Link } from 'react-router';
import { EventImage } from '../../../shared/ui/EventImage';
import { Icon } from '../../../shared/ui/Icon';
import { formatPrice, formatTaipei } from '../../../shared/utils/format';
import { describeSalesStatus } from '../model/describeSalesStatus';

import type { EventDetailDto } from '../model/EventDetailDto';

export function EventHero({
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
    <div className="detail-hero">
      <div className="detail-hero__image">
        <EventImage
          src={data.imagePath}
          alt={data.title}
          fetchPriority="high"
        />
      </div>
      <div className="detail-hero__content">
        <div className="detail-hero__badges">
          <span className={`badge badge--${data.category.toLowerCase()}`}>
            {data.genre}
          </span>
          <span
            className={`status status--${performance.salesStatus.toLowerCase()}`}
          >
            {describeSalesStatus(performance.salesStatus)}
          </span>
        </div>
        <h1>{data.title}</h1>
        <p className="detail__performer">{data.performer}</p>
        <dl className="event-facts">
          <div>
            <dt>活動時間</dt>
            <dd>{formatTaipei(performance.startsAtUtc)}</dd>
          </div>
          <div>
            <dt>活動地點</dt>
            <dd>
              {performance.venue}
              <span>{performance.city}</span>
            </dd>
          </div>
          <div>
            <dt>演出長度</dt>
            <dd>約 {performance.durationMinutes} 分鐘</dd>
          </div>
        </dl>
        <div className="detail-hero__purchase">
          <div>
            <span className="muted">票價</span>
            <strong>
              {formatPrice(minPrice, data.currency)} <small>起</small>
            </strong>
          </div>
          {onSale ? (
            <Link className="cta" to={purchaseLink}>
              立即購票
              <Icon name="arrow" size={18} />
            </Link>
          ) : (
            <span className="cta cta--disabled">
              {describeSalesStatus(performance.salesStatus)}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
