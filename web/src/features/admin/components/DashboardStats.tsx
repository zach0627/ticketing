import type { DashboardDto } from '../model/DashboardDto';

export function DashboardStats({ data }: { data: DashboardDto }) {
  return (
    <>
      <dl className="detail__facts">
        <div>
          <dt>有效保留</dt>
          <dd>{data.activeHolds}</dd>
        </div>
        <div>
          <dt>已售座位</dt>
          <dd>{data.soldSeats}</dd>
        </div>
        <div>
          <dt>訂單</dt>
          <dd>{data.orders}</dd>
        </div>
      </dl>
    </>
  );
}
