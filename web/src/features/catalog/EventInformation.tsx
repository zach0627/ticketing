import type { EventDetailDto } from '../../shared/api/types';
import { Icon } from '../../shared/ui/Icon';
import {
  describeSalesStatus,
  formatPrice,
  formatTaipei,
} from '../../shared/utils/format';

export function EventInformation({ data }: { data: EventDetailDto }) {
  const { performance } = data;
  return (
    <div className="detail-main">
      <section id="about-event">
        <h2>活動介紹</h2>
        <p className="detail__description">{data.description}</p>
      </section>
      <section id="ticket-info">
        <h2>場次與票價</h2>
        <div className="performance-card">
          <Icon name="calendar" size={24} />
          <div>
            <strong>{formatTaipei(performance.startsAtUtc)}</strong>
            <span>
              {performance.venue} · {performance.city}
            </span>
          </div>
          <span
            className={`status status--${performance.salesStatus.toLowerCase()}`}
          >
            {describeSalesStatus(performance.salesStatus)}
          </span>
        </div>
        <div className="table-scroll">
          <table className="sections">
            <thead>
              <tr>
                <th>票區</th>
                <th>票價</th>
                <th>座位數</th>
              </tr>
            </thead>
            <tbody>
              {data.sections.map((s) => (
                <tr key={s.id}>
                  <td>
                    <span className="zone-dot" />
                    {s.code} 區 · {s.name}
                  </td>
                  <td>
                    <strong>{formatPrice(s.price, data.currency)}</strong>
                  </td>
                  <td>{s.rowCount * s.seatsPerRow} 席</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="detail-caption">
          座位數為票區總容量；剩餘座位請以選位頁顯示為準。
        </p>
      </section>
      <section id="purchase-notes">
        <h2>購票須知</h2>
        <ul className="purchase-notes">
          <li>
            每人每場最多購買 <strong>{data.maxTicketsPerBuyer} 張</strong>
            ，已付款票券也計入上限。
          </li>
          <li>
            每筆保留限同一票區。
            {data.allowsContiguousAllocation
              ? '本場支援手動選位與自動連號配位。'
              : '本場採手動劃位，請點選喜歡的座位。'}
          </li>
          <li>成功保留後有五分鐘可確認與付款，到期後座位會重新開放。</li>
          <li>開賣時間：{formatTaipei(performance.salesOpensAtUtc)}。</li>
          <li>售票截止：{formatTaipei(performance.salesClosesAtUtc)}。</li>
        </ul>
        <p className="notice">
          <Icon name="info" size={18} />
          <span>
            本活動、演出者與場館皆為虛構。付款為模擬流程，
            <strong>票券無實際入場效力</strong>。
          </span>
        </p>
      </section>
    </div>
  );
}
