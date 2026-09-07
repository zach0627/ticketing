import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from './api';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import type { SeatDto } from '../../shared/api/types';
import { describeSalesStatus, formatPrice } from '../../shared/utils/format';

/** 把一個票區的座位排成「排 → 座位」的二維結構。 */
function groupByRow(seats: SeatDto[]): [number, SeatDto[]][] {
  const rows = new Map<number, SeatDto[]>();
  for (const seat of seats) {
    const row = rows.get(seat.rowNumber);
    if (row) row.push(seat);
    else rows.set(seat.rowNumber, [seat]);
  }
  return [...rows.entries()].sort(([a], [b]) => a - b);
}

export function SeatMapPage() {
  const { performanceId = '' } = useParams();
  const id = Number(performanceId);

  const { data, isPending, error } = useQuery({
    queryKey: ['seatmap', id],
    queryFn: ({ signal }) => catalogApi.getSeatMap(id, signal),
  });

  if (isPending) return <Spinner />;
  if (error) return <ErrorMessage error={error} />;

  const available = data.seats.filter((s) => s.status === 'Available').length;

  return (
    <article className="seatmap">
      <Link className="back-link" to={`/events/${data.eventCode}`}>← 回到活動頁</Link>

      <h1>{data.eventTitle}</h1>
      <p className="seatmap__summary">
        {describeSalesStatus(data.salesStatus)}・剩餘 {available} / {data.seats.length} 席
        ・每人最多 {data.maxTicketsPerBuyer} 張
      </p>

      <p className="notice">
        階段 4 的座位圖是<strong>唯讀</strong>的，選位與保留在階段 6 開放。
      </p>

      <ul className="legend">
        <li><span className="seat seat--available" /> 可選</li>
        <li><span className="seat seat--held" /> 保留中</li>
        <li><span className="seat seat--sold" /> 已售出</li>
      </ul>

      {data.sections.map((section) => (
        <section key={section.id} className="zone">
          <h2>
            {section.code} 區・{section.name}
            <span className="zone__price">{formatPrice(section.price)}</span>
          </h2>

          <div className="stage">舞台／場地</div>

          {groupByRow(data.seats.filter((s) => s.sectionId === section.id)).map(([row, seats]) => (
            <div key={row} className="row">
              <span className="row__label">{row} 排</span>
              {seats.map((seat) => (
                <span
                  key={seat.id}
                  className={`seat seat--${seat.status.toLowerCase()}`}
                  title={`${section.code} 區 ${seat.rowNumber} 排 ${seat.seatNumber} 號`}
                >
                  {seat.seatNumber}
                </span>
              ))}
            </div>
          ))}
        </section>
      ))}
    </article>
  );
}
