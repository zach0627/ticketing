import { useState } from 'react';
import { Link } from 'react-router';
import type { EventCardDto } from '../../shared/api/types';
import { Icon } from '../../shared/ui/Icon';
import { EventImage } from '../../shared/ui/EventImage';
import {
  describeSalesStatus,
  formatCalendarDate,
} from '../../shared/utils/format';
import './featured.css';

export function FeaturedEvents({ events }: { events: EventCardDto[] }) {
  const preferred = ['C01', 'C10', 'C02'];
  const featured = preferred
    .map((code) => events.find((event) => event.code === code))
    .filter((event): event is EventCardDto => !!event);
  const slides = featured.length ? featured : events.slice(0, 3);
  const [index, setIndex] = useState(0);
  if (!slides.length) return null;
  const current = index % slides.length;
  const event = slides[current];
  const previous = slides[(current + slides.length - 1) % slides.length];
  const next = slides[(current + 1) % slides.length];
  const move = (offset: number) =>
    setIndex((current + slides.length + offset) % slides.length);
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
