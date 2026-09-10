import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { formatPrice, formatTaipei } from '../../../shared/utils/format';

import type { PagedResult } from '../../../shared/api/PagedResult';
import type { AdminOrderDto } from '../model/AdminOrderDto';

export function AdminOrders({
  performanceId,
  onClearFilter,
  orders,
  isPending,
  error,
}: {
  performanceId: number | null;
  onClearFilter: () => void;
  orders: PagedResult<AdminOrderDto> | undefined;
  isPending: boolean;
  error: Error | null;
}) {
  return (
    <>
      <h2>
        訂單
        {performanceId && (
          <button
            className="site-header__link-button"
            type="button"
            onClick={onClearFilter}
          >
            （顯示全部）
          </button>
        )}
      </h2>

      {isPending && <Spinner />}
      {error && <ErrorMessage error={error} />}
      {orders && (
        <div
          className="table-scroll"
          role="region"
          aria-label="管理資料表"
          tabIndex={0}
        >
          <table className="sections">
            <thead>
              <tr>
                <th>活動</th>
                <th>買家</th>
                <th>張數</th>
                <th>金額</th>
                <th>下單時間</th>
              </tr>
            </thead>
            <tbody>
              {orders.items.map((order) => (
                <tr key={order.id}>
                  <td>{order.eventTitle}</td>
                  <td>{order.buyerDisplayName}</td>
                  <td>{order.quantity}</td>
                  <td>{formatPrice(order.totalAmount, order.currency)}</td>
                  <td>{formatTaipei(order.createdAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
