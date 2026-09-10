import { Link } from 'react-router';
import { EventImage } from '../../../shared/ui/EventImage';
import { Icon } from '../../../shared/ui/Icon';
import { formatCalendarDate } from '../../../shared/utils/format';
import { useFeaturedEvents } from '../hooks/useFeaturedEvents';
import type { EventCardDto } from '../model/EventCardDto';
import { describeSalesStatus } from '../model/describeSalesStatus';
import '../styles/featured.css';

export function FeaturedEvents({ events }: { events: EventCardDto[] }) {
  const { slides, current, event, previous, next, move, setIndex } =
    useFeaturedEvents(events);
  if (!slides.length) return null;
  return (
    <section
      className="featured-carousel"
      aria-label="精選活動"
      aria-roledescription="輪播"
    >
      <div className="featured-track">
        <button
          type="button"
          className="featured-preview"
          aria-label={`上一場：${previous.title}`}
          onClick={() => move(-1)}
          tabIndex={-1}
        >
          <EventImage src={previous.imagePath} alt="" />
        </button>
        <Link
          className="featured-banner"
          to={`/events/${event.code}`}
          aria-label={`查看 ${event.title}`}
        >
          <EventImage src={event.imagePath} alt="" fetchPriority="high" />
          <div className="featured-caption" aria-live="polite">
            <h1>{event.title}</h1>
            <div>
              <span>
                {formatCalendarDate(event.startsAtUtc)} · {event.city}
              </span>
              <span>
                {describeSalesStatus(event.salesStatus)}
                <Icon name="chevron" size={16} />
              </span>
            </div>
          </div>
        </Link>
        <button
          type="button"
          className="featured-preview"
          aria-label={`下一場：${next.title}`}
          onClick={() => move(1)}
          tabIndex={-1}
        >
          <EventImage src={next.imagePath} alt="" />
        </button>
      </div>
      <button
        className="carousel-arrow carousel-arrow--previous"
        type="button"
        aria-label="上一個精選活動"
        onClick={() => move(-1)}
      >
        <Icon
          name="chevron"
          size={25}
          style={{ transform: 'rotate(180deg)' }}
        />
      </button>
      <button
        className="carousel-arrow carousel-arrow--next"
        type="button"
        aria-label="下一個精選活動"
        onClick={() => move(1)}
      >
        <Icon name="chevron" size={25} />
      </button>
      <div className="carousel-dots">
        {slides.map((slide, i) => (
          <button
            type="button"
            key={slide.code}
            aria-label={`查看精選活動 ${i + 1}：${slide.performer}`}
            aria-pressed={i === current}
            className={i === current ? 'is-active' : ''}
            onClick={() => setIndex(i)}
          >
            <span />
          </button>
        ))}
      </div>
    </section>
  );
}
