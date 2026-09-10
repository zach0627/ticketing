import { Icon } from '../../../shared/ui/Icon';
import { Empty } from '../../../shared/ui/States';
import { EventCard } from '../components/EventCard';

import type { EventCardDto } from '../model/EventCardDto';

export function CatalogResults({
  hasData,
  events,
  params,
  clearFilters,
}: {
  hasData: boolean;
  events: EventCardDto[];
  params: URLSearchParams;
  clearFilters: () => void;
}) {
  return (
    <>
      {hasData && (
        <div className="catalog-results">
          <p className="result-count" role="status">
            找到 <strong>{events.length}</strong> 場活動
            {params.get('q')?.trim() && (
              <>・「{params.get('q')!.trim()}」的搜尋結果</>
            )}
          </p>
          {['category', 'q', 'city', 'sale', 'sort'].some(
            (key) => !!params.get(key),
          ) && (
            <button
              className="clear-filters"
              type="button"
              onClick={clearFilters}
            >
              清除篩選
            </button>
          )}
        </div>
      )}
      {hasData && events.length === 0 && (
        <Empty>
          <Icon name="search" size={34} />
          <strong>還沒找到符合的活動</strong>
          <span>試試其他關鍵字，或放寬篩選條件。</span>
          <button
            type="button"
            className="cta cta--secondary"
            onClick={clearFilters}
          >
            查看全部活動
          </button>
        </Empty>
      )}
      {events.length > 0 && (
        <ul className="card-grid">
          {events.map((event) => (
            <li key={event.id}>
              <EventCard event={event} />
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
