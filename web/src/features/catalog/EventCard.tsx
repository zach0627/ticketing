import { Link } from 'react-router';
import type { EventCardDto } from '../../shared/api/types';
import { describeSalesStatus, formatPrice, formatTaipei } from '../../shared/utils/format';

export function EventCard({ event }: { event: EventCardDto }) {
  return (
    <Link className="card" to={`/events/${event.code}`}>
      <img className="card__image" src={event.imagePath} alt={event.title} loading="lazy" />
      <div className="card__body">
        <span className={`badge badge--${event.category.toLowerCase()}`}>
          {event.category === 'Concert' ? '演唱會' : '運動賽事'}
        </span>
        <h2 className="card__title">{event.title}</h2>
        <p className="card__performer">{event.performer}</p>
        <p className="card__meta">
          {formatTaipei(event.startsAtUtc)}・{event.city}
        </p>
        <p className="card__footer">
          <span className="card__price">{formatPrice(event.minPrice, event.currency)} 起</span>
          <span className={`status status--${event.salesStatus.toLowerCase()}`}>
            {describeSalesStatus(event.salesStatus)}
          </span>
        </p>
      </div>
    </Link>
  );
}
