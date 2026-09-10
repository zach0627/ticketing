import { Link } from 'react-router';
import type { EventCardDto } from '../../shared/api/types';
import { formatCalendarDate, formatPrice } from '../../shared/utils/format';
import { Icon } from '../../shared/ui/Icon';
import { EventImage } from '../../shared/ui/EventImage';
import './eventCard.css';

export function EventCard({ event }: { event: EventCardDto }) {
  return (
    <Link className="event-card" to={`/events/${event.code}`}>
      <EventImage
        className="event-card__image"
        src={event.imagePath}
        alt=""
        loading="lazy"
      />
      <div className="event-card__body">
        <h3 className="event-card__title">{event.title}</h3>
        <div className="event-card__meta">
          <span>
            <Icon name="calendar" size={16} />
            {formatCalendarDate(event.startsAtUtc)}
          </span>
          <span className="event-card__price">
            {formatPrice(event.minPrice, event.currency)} 起
          </span>
        </div>
      </div>
    </Link>
  );
}
