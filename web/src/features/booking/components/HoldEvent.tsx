import { EventImage } from '../../../shared/ui/EventImage';
import { formatTaipei } from '../../../shared/utils/format';

import type { EventDetailDto } from '../../catalog';

export function HoldEvent({ event }: { event: EventDetailDto | undefined }) {
  if (!event) return null;
  return (
    <section className="panel hold-event">
      <EventImage src={event.imagePath} alt={event.title} />
      <div>
        <span className="badge">{event.genre}</span>
        <h2>{event.title}</h2>
        <p>{formatTaipei(event.performance.startsAtUtc)}</p>
        <p>
          {event.performance.venue} · {event.performance.city}
        </p>
      </div>
    </section>
  );
}
