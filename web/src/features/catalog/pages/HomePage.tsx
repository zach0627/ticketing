import '../styles/catalog.css';
import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { CatalogResults } from '../components/CatalogResults';
import { CatalogSearch } from '../components/CatalogSearch';
import { CatalogToolbar } from '../components/CatalogToolbar';
import { FeaturedEvents } from '../components/FeaturedEvents';
import { useCatalog } from '../hooks/useCatalog';

export function HomePage() {
  const {
    data,
    isPending,
    error,
    refetch,
    events,
    params,
    update,
    clearFilters,
  } = useCatalog();
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
          <CatalogResults
            hasData={!!data}
            events={events}
            params={params}
            clearFilters={clearFilters}
          />
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
