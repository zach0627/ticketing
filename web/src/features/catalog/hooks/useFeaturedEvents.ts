import { useState } from 'react';
import type { EventCardDto } from '../model/EventCardDto';

export function useFeaturedEvents(events: EventCardDto[]) {
  const preferred = ['C01', 'C10', 'C02'];
  const featured = preferred
    .map((code) => events.find((event) => event.code === code))
    .filter((event): event is EventCardDto => !!event);
  const slides = featured.length ? featured : events.slice(0, 3);
  const [index, setIndex] = useState(0);
  const current = index % (slides.length || 1);
  const event = slides[current];
  const previous = slides[(current + slides.length - 1) % slides.length];
  const next = slides[(current + 1) % slides.length];
  const move = (offset: number) =>
    setIndex((current + slides.length + offset) % slides.length);
  return { slides, current, event, previous, next, move, setIndex };
}
