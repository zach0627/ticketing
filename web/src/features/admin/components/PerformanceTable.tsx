import { formatTaipei } from '../../../shared/utils/format';
import { describeSalesStatus } from '../../catalog';

import type { AdminPerformanceDto } from '../model/AdminPerformanceDto';

export function PerformanceTable({
  performances,
  busy,
  onTogglePause,
  onSelectPerformance,
}: {
  performances: AdminPerformanceDto[];
  busy: boolean;
  onTogglePause: (value: { id: number; paused: boolean }) => void;
  onSelectPerformance: (id: number) => void;
}) {
  return (
    <>
      <h2>場次</h2>
      <div
        className="table-scroll"
        role="region"
        aria-label="管理資料表"
        tabIndex={0}
      >
        <table className="sections">
          <thead>
            <tr>
              <th>代碼</th>
              <th>活動</th>
              <th>開演</th>
              <th>狀態</th>
              <th>售票</th>
              <th>訂單</th>
            </tr>
          </thead>
          <tbody>
            {performances.map((performance) => (
              <tr key={performance.id}>
                <td>{performance.eventCode}</td>
                <td>{performance.eventTitle}</td>
                <td>{formatTaipei(performance.startsAtUtc)}</td>
                <td>{describeSalesStatus(performance.salesStatus)}</td>
                <td>
                  <button
                    className="site-header__link-button"
                    type="button"
                    disabled={busy}
                    onClick={() =>
                      onTogglePause({
                        id: performance.id,
                        paused: !performance.isSalesPaused,
                      })
                    }
                  >
                    {performance.isSalesPaused ? '恢復售票' : '暫停售票'}
                  </button>
                </td>
                <td>
                  <button
                    className="site-header__link-button"
                    type="button"
                    onClick={() => onSelectPerformance(performance.id)}
                  >
                    只看這場
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
