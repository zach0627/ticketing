import '../styles/eventDetail.css';
import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { EventHero } from '../components/EventHero';
import { EventInformation } from '../components/EventInformation';
import { EventMobilePurchase } from '../components/EventMobilePurchase';
import { EventPurchaseInfo } from '../components/EventPurchaseInfo';
import { EventTabs } from '../components/EventTabs';
import { useEventDetail } from '../hooks/useEventDetail';

export function EventDetailPage() {
  const { data, isPending, error, refetch } = useEventDetail();
  if (isPending) return <Spinner />;
  if (error)
    return <ErrorMessage error={error} onRetry={() => void refetch()} />;
  const { performance } = data;
  const onSale = performance.salesStatus === 'OnSale';
  const minPrice = Math.min(...data.sections.map((s) => s.price));
  const purchaseLink = `/performances/${performance.id}/seats`;

  return (
    <article className="detail">
      <nav className="breadcrumb" aria-label="麵包屑">
        <Link to="/">首頁</Link>
        <Icon name="chevron" size={12} />
        <Link to={`/?category=${data.category}#events`}>
          {data.category === 'Concert' ? '演唱會' : '運動賽事'}
        </Link>
        <Icon name="chevron" size={12} />
        <span>活動詳情</span>
      </nav>
      <EventHero
        data={data}
        onSale={onSale}
        minPrice={minPrice}
        purchaseLink={purchaseLink}
      />
      <EventTabs />
      <div className="detail-layout">
        <EventInformation data={data} />
        <EventPurchaseInfo
          data={data}
          onSale={onSale}
          minPrice={minPrice}
          purchaseLink={purchaseLink}
        />
      </div>
      <EventMobilePurchase
        data={data}
        onSale={onSale}
        minPrice={minPrice}
        purchaseLink={purchaseLink}
      />
    </article>
  );
}
