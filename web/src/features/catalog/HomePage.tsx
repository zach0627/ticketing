import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from './api';
import { EventCard } from './EventCard';
import { Empty, ErrorMessage, Spinner } from '../../shared/ui/States';

type Filter = 'All' | 'Concert' | 'Sport';

const filters: { key: Filter; label: string }[] = [
  { key: 'All', label: '全部' },
  { key: 'Concert', label: '演唱會' },
  { key: 'Sport', label: '運動賽事' },
];

export function HomePage() {
  const [filter, setFilter] = useState<Filter>('All');

  const { data, isPending, error } = useQuery({
    queryKey: ['events', filter],
    queryFn: ({ signal }) => catalogApi.listEvents(filter === 'All' ? undefined : filter, signal),
  });

  return (
    <>
      <header className="page-header">
        <h1>熱門活動</h1>
        <p className="page-header__note">
          本站為作品展示，付款為模擬流程，<strong>票券無實際入場效力</strong>。
        </p>
      </header>

      <nav className="filters" aria-label="活動類別">
        {filters.map((f) => (
          <button
            key={f.key}
            type="button"
            className={`filters__button${filter === f.key ? ' is-active' : ''}`}
            aria-pressed={filter === f.key}
            onClick={() => setFilter(f.key)}
          >
            {f.label}
          </button>
        ))}
      </nav>

      {isPending && <Spinner />}
      {error && <ErrorMessage error={error} />}
      {data && data.items.length === 0 && <Empty>目前沒有符合條件的活動。</Empty>}

      {data && data.items.length > 0 && (
        <>
          <p className="result-count">共 {data.total} 場</p>
          <ul className="card-grid">
            {data.items.map((event) => (
              <li key={event.id}>
                <EventCard event={event} />
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  );
}
