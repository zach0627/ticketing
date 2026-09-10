import './catalog.css';
import { Link, useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from './api';
import { EventCard } from './EventCard';
import { CatalogToolbar } from './CatalogToolbar';
import { CatalogSearch } from './CatalogSearch';
import { FeaturedEvents } from './FeaturedEvents';
import { filterEvents } from './catalogFilters';
import { Empty, ErrorMessage, Spinner } from '../../shared/ui/States';
import { Icon } from '../../shared/ui/Icon';

export function HomePage() {
  const [params, setParams] = useSearchParams();
  const { data, isPending, error, refetch } = useQuery({
    queryKey: ['events'],
    queryFn: ({ signal }) => catalogApi.listEvents(undefined, signal),
  });
  const events = data ? filterEvents(data.items, params) : [];
  function update(key: string, value: string) {
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value) next.set(key, value);
        else next.delete(key);
        return next;
      },
      { replace: true, preventScrollReset: true },
    );
  }
  return (
    <div className="home-page">
      {data && <FeaturedEvents events={data.items} />}
      {isPending && (
        <div className="featured-placeholder">
          <h1>探索精彩活動</h1>
          <Spinner label="正在為你準備精彩活動…" />
        </div>
      )}
      <div className="container">
        <CatalogSearch
          value={params.get('q') ?? ''}
          onChange={(value) => update('q', value)}
        />
        <div className="service-note">
          <Icon name="info" size={17} />
          <span>
            活動皆為虛構，付款為模擬流程，<strong>票券無實際入場效力</strong>。
          </span>
          <Link to="/guide">
            了解購票流程
            <Icon name="chevron" size={14} />
          </Link>
        </div>
        <section
          id="events"
          className="events-section"
          aria-labelledby="events-heading"
        >
          <h2 id="events-heading" className="sr-only">
            活動列表
          </h2>
          <CatalogToolbar
            items={data?.items ?? []}
            params={params}
            onChange={update}
          />
          {error && (
            <ErrorMessage error={error} onRetry={() => void refetch()} />
          )}
          {data && (
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
                  onClick={() => setParams({}, { preventScrollReset: true })}
                >
                  清除篩選
                </button>
              )}
            </div>
          )}
          {data && events.length === 0 && (
            <Empty>
              <Icon name="search" size={34} />
              <strong>還沒找到符合的活動</strong>
              <span>試試其他關鍵字，或放寬篩選條件。</span>
              <button
                type="button"
                className="cta cta--secondary"
                onClick={() => setParams({}, { preventScrollReset: true })}
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
        </section>
        <div className="catalog-help">
          <Link to="/guide">
            購票流程與常見問題
            <Icon name="chevron" size={16} />
          </Link>
        </div>
      </div>
    </div>
  );
}
