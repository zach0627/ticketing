import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from './api';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { describeSalesStatus, formatPrice, formatTaipei } from '../../shared/utils/format';

export function EventDetailPage() {
  const { code = '' } = useParams();

  const { data, isPending, error } = useQuery({
    queryKey: ['event', code],
    queryFn: ({ signal }) => catalogApi.getEvent(code, signal),
  });

  if (isPending) return <Spinner />;
  if (error) return <ErrorMessage error={error} />;

  const { performance } = data;
  const onSale = performance.salesStatus === 'OnSale';

  return (
    <article className="detail">
      <Link className="back-link" to="/">← 回到活動列表</Link>

      <img className="detail__image" src={data.imagePath} alt={data.title} />

      <h1>{data.title}</h1>
      <p className="detail__performer">{data.performer}・{data.genre}</p>
      <p className="detail__description">{data.description}</p>

      <dl className="detail__facts">
        <div><dt>時間</dt><dd>{formatTaipei(performance.startsAtUtc)}</dd></div>
        <div><dt>場館</dt><dd>{performance.venue}（{performance.city}）</dd></div>
        <div><dt>長度</dt><dd>{performance.durationMinutes} 分鐘</dd></div>
        <div><dt>售票狀態</dt><dd>{describeSalesStatus(performance.salesStatus)}</dd></div>
        <div>
          <dt>每人上限</dt>
          <dd>
            {data.maxTicketsPerBuyer} 張
            {data.allowsContiguousAllocation ? '（可自動連號配位）' : '（需自行選位）'}
          </dd>
        </div>
      </dl>

      <h2>票區與票價</h2>
      <table className="sections">
        <thead>
          <tr><th>票區</th><th>名稱</th><th>票價</th><th>座位數</th></tr>
        </thead>
        <tbody>
          {data.sections.map((s) => (
            <tr key={s.id}>
              <td>{s.code}</td>
              <td>{s.name}</td>
              <td>{formatPrice(s.price, data.currency)}</td>
              <td>{s.rowCount * s.seatsPerRow}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {onSale ? (
        <Link className="cta" to={`/performances/${performance.id}/seats`}>查看座位圖</Link>
      ) : (
        <p className="cta cta--disabled">
          {describeSalesStatus(performance.salesStatus)}
          {performance.salesStatus === 'NotYetOnSale' &&
            `，${formatTaipei(performance.salesOpensAtUtc)} 開賣`}
        </p>
      )}
    </article>
  );
}
