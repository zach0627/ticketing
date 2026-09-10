import './eventDetail.css';
import { EventInformation } from './EventInformation';
import { EventTabs } from './EventTabs';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from './api';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { Icon } from '../../shared/ui/Icon';
import { EventImage } from '../../shared/ui/EventImage';
import {
  describeSalesStatus,
  formatPrice,
  formatTaipei,
} from '../../shared/utils/format';

export function EventDetailPage() {
  const { code = '' } = useParams();
  const { data, isPending, error, refetch } = useQuery({
    queryKey: ['event', code],
    queryFn: ({ signal }) => catalogApi.getEvent(code, signal),
  });
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
      <div className="detail-hero">
        <div className="detail-hero__image">
          <EventImage
            src={data.imagePath}
            alt={data.title}
            fetchPriority="high"
          />
        </div>
        <div className="detail-hero__content">
          <div className="detail-hero__badges">
            <span className={`badge badge--${data.category.toLowerCase()}`}>
              {data.genre}
            </span>
            <span
              className={`status status--${performance.salesStatus.toLowerCase()}`}
            >
              {describeSalesStatus(performance.salesStatus)}
            </span>
          </div>
          <h1>{data.title}</h1>
          <p className="detail__performer">{data.performer}</p>
          <dl className="event-facts">
            <div>
              <dt>活動時間</dt>
              <dd>{formatTaipei(performance.startsAtUtc)}</dd>
            </div>
            <div>
              <dt>活動地點</dt>
              <dd>
                {performance.venue}
                <span>{performance.city}</span>
              </dd>
            </div>
            <div>
              <dt>演出長度</dt>
              <dd>約 {performance.durationMinutes} 分鐘</dd>
            </div>
          </dl>
          <div className="detail-hero__purchase">
            <div>
              <span className="muted">票價</span>
              <strong>
                {formatPrice(minPrice, data.currency)} <small>起</small>
              </strong>
            </div>
            {onSale ? (
              <Link className="cta" to={purchaseLink}>
                立即購票
                <Icon name="arrow" size={18} />
              </Link>
            ) : (
              <span className="cta cta--disabled">
                {describeSalesStatus(performance.salesStatus)}
              </span>
            )}
          </div>
        </div>
      </div>
      <EventTabs />
      <div className="detail-layout">
        <EventInformation data={data} />
        <aside className="detail-sidebar">
          <h2>購票資訊</h2>
          <p>
            選好喜歡的票區，
            <br />
            前往座位圖開始選位。
          </p>
          <div className="detail-sidebar__price">
            {formatPrice(minPrice, data.currency)} <small>起</small>
          </div>
          {onSale ? (
            <Link className="cta" to={purchaseLink}>
              選擇座位
              <Icon name="arrow" size={17} />
            </Link>
          ) : (
            <p className="notice">
              {performance.salesStatus === 'NotYetOnSale'
                ? `${formatTaipei(performance.salesOpensAtUtc)} 開賣`
                : describeSalesStatus(performance.salesStatus)}
            </p>
          )}
          <small>
            <Icon name="shield" size={14} />
            每人限購 {data.maxTicketsPerBuyer} 張
          </small>
        </aside>
      </div>
      <div className="detail-mobile-purchase">
        <div>
          <span>{describeSalesStatus(performance.salesStatus)}</span>
          <strong>
            {formatPrice(minPrice, data.currency)} <small>起</small>
          </strong>
        </div>
        {onSale ? (
          <Link className="cta" to={purchaseLink}>
            立即購票
            <Icon name="arrow" size={17} />
          </Link>
        ) : (
          <span className="cta cta--disabled">
            {describeSalesStatus(performance.salesStatus)}
          </span>
        )}
      </div>
    </article>
  );
}
