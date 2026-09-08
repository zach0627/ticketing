import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ordersApi } from './api';
import { Empty, ErrorMessage, Spinner } from '../../shared/ui/States';
import { formatPrice, formatTaipei } from '../../shared/utils/format';

export function OrdersPage() {
  const { data, isPending, error } = useQuery({
    queryKey: ['orders'],
    queryFn: ({ signal }) => ordersApi.list(signal),
  });

  if (isPending) return <Spinner />;
  if (error) return <ErrorMessage error={error} />;

  return (
    <section>
      <h1>我的訂單</h1>
      <p className="page-header__note">票券無實際入場效力。</p>

      {data.items.length === 0 && <Empty>你還沒有任何訂單。</Empty>}

      {data.items.length > 0 && (
        <table className="sections">
          <thead>
            <tr><th>活動</th><th>演出時間</th><th>張數</th><th>金額</th><th>下單時間</th></tr>
          </thead>
          <tbody>
            {data.items.map((order) => (
              <tr key={order.id}>
                <td><Link to={`/orders/${order.id}`}>{order.eventTitle}</Link></td>
                <td>{formatTaipei(order.startsAtUtc)}</td>
                <td>{order.quantity}</td>
                <td>{formatPrice(order.totalAmount, order.currency)}</td>
                <td>{formatTaipei(order.createdAtUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
