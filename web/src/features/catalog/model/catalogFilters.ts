import type { EventCardDto } from './EventCardDto';

/** 篩選目前已完整載入的 catalog，不改寫 Query cache。 */
export function filterEvents(
  events: EventCardDto[],
  params: URLSearchParams,
): EventCardDto[] {
  const category = params.get('category');
  const city = events.some((event) => event.city === params.get('city'))
    ? params.get('city')
    : null;
  const sale = params.get('sale');
  const query = (params.get('q') ?? '')
    .normalize('NFKC')
    .trim()
    .toLocaleLowerCase();
  const result = events.filter(
    (event) =>
      (!['Concert', 'Sport'].includes(category ?? '') ||
        event.category === category) &&
      (!city || event.city === city) &&
      (!['OnSale', 'NotYetOnSale', 'Paused', 'SalesClosed'].includes(
        sale ?? '',
      ) ||
        event.salesStatus === sale) &&
      (!query ||
        `${event.title} ${event.performer} ${event.city}`
          .normalize('NFKC')
          .toLocaleLowerCase()
          .includes(query)),
  );
  return result.sort((a, b) => {
    const dateOrder =
      Date.parse(a.startsAtUtc) - Date.parse(b.startsAtUtc) || a.id - b.id;
    return params.get('sort') === 'price'
      ? a.minPrice - b.minPrice || dateOrder
      : dateOrder;
  });
}
