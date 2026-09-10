import { formatPrice } from '../../../shared/utils/format';

import type { HoldDto } from '../model/HoldDto';

export function HoldSeats({ data }: { data: HoldDto }) {
  return (
    <section className="panel">
      <h2>
        座位明細 <span className="muted">/ {data.items.length} 張</span>
      </h2>
      <div className="table-scroll">
        <table className="sections">
          <thead>
            <tr>
              <th>票區</th>
              <th>座位</th>
              <th>票價</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((item) => (
              <tr key={item.seatId}>
                <td>{item.sectionCode} 區</td>
                <td>
                  {item.rowNumber} 排 {item.seatNumber} 號
                </td>
                <td>{formatPrice(item.unitPrice, data.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
