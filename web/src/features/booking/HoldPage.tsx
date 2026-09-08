import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { bookingApi } from './api';
import { formatRemaining, useCountdown } from './useCountdown';
import { useAuth } from '../auth/authContext';
import { ApiError } from '../../shared/api/http';
import { newIdempotencyKey, pendingOperations } from '../../shared/api/idempotency';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { formatPrice } from '../../shared/utils/format';

const statusText: Record<string, string> = {
  Active: '保留中',
  Completed: '已完成付款',
  Cancelled: '已取消',
  Expired: '已過期',
};

export function HoldPage() {
  const { holdId = '' } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();

  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isPending, error: loadError, refetch } = useQuery({
    queryKey: ['hold', holdId],
    // 每次進頁都重抓：重播回來的資料可能是幾分鐘前的，
    // 倒數要用**最新**的 serverNowUtc 當基準，不能拿舊的重新起算五分鐘
    queryFn: ({ signal }) => bookingApi.getHold(holdId, signal),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });

  // 重新整理後恢復：上一次還沒確定結果的付款，用原本的 key 重送
  useEffect(() => {
    if (!user) return;

    const pending = pendingOperations.read(user.id, 'checkout', holdId);
    if (!pending) return;

    bookingApi
      .checkout(holdId, (pending.payload as 'Succeeded' | 'Failed') ?? 'Succeeded', pending.key)
      .then((order) => {
        pendingOperations.clear();
        navigate(`/orders/${order.id}`, { replace: true });
      })
      .catch(() => {
        pendingOperations.clear();
        void refetch();
      });
  }, [user, holdId, navigate, refetch]);

  const remaining = useCountdown(data?.serverNowUtc ?? '', data?.expiresAtUtc ?? '');

  if (isPending) return <Spinner />;
  if (loadError) return <ErrorMessage error={loadError} />;

  // 已完成的保留只是一個入口，真正的內容在訂單頁
  if (data.status === 'Completed' && data.orderId) {
    return (
      <section className="hold">
        <h1>這筆保留已經完成付款</h1>
        <Link className="cta" to={`/orders/${data.orderId}`}>查看訂單</Link>
      </section>
    );
  }

  const isActive = data.status === 'Active' && remaining > 0;

  async function pay(outcome: 'Succeeded' | 'Failed') {
    if (!user) return;

    const key = newIdempotencyKey();
    pendingOperations.save({ userId: user.id, operation: 'checkout', target: holdId, key, payload: outcome });

    setError(null);
    setBusy(true);

    try {
      const order = await bookingApi.checkout(holdId, outcome, key);
      pendingOperations.clear();
      navigate(`/orders/${order.id}`);
    } catch (caught) {
      pendingOperations.clear();

      // 402 是「模擬付款失敗」：留在這一頁，用**新的 key** 再試一次
      setError(caught instanceof ApiError ? caught.message : '付款失敗，請再試一次。');
      await refetch();
    } finally {
      setBusy(false);
    }
  }

  async function cancel() {
    setBusy(true);
    try {
      await bookingApi.cancel(holdId);
      navigate(`/performances/${data!.performanceId}/seats`, { replace: true });
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : '取消失敗，請再試一次。');
      await refetch();
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="hold">
      <h1>確認你的座位</h1>

      <p className={`hold__status hold__status--${data.status.toLowerCase()}`}>
        {statusText[data.status] ?? data.status}
        {isActive && <> ・剩餘 <strong>{formatRemaining(remaining)}</strong></>}
      </p>

      {!isActive && data.status === 'Active' && (
        <p className="notice">
          倒數已經歸零。實際是否過期由<strong>伺服器</strong>判定——按付款會得到明確的答覆。
        </p>
      )}

      <table className="sections">
        <thead>
          <tr><th>票區</th><th>排</th><th>座號</th><th>票價</th></tr>
        </thead>
        <tbody>
          {data.items.map((item) => (
            <tr key={item.seatId}>
              <td>{item.sectionCode}</td>
              <td>{item.rowNumber}</td>
              <td>{item.seatNumber}</td>
              <td>{formatPrice(item.unitPrice, data.currency)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <p className="hold__total">合計 {formatPrice(data.totalAmount, data.currency)}</p>

      {error && <p className="auth-form__error" role="alert">{error}</p>}

      {data.status === 'Active' ? (
        <div className="hold__actions">
          <button className="cta" type="button" disabled={busy} onClick={() => pay('Succeeded')}>
            模擬付款成功
          </button>
          <button className="cta cta--secondary" type="button" disabled={busy} onClick={() => pay('Failed')}>
            模擬付款失敗
          </button>
          <button className="cta cta--secondary" type="button" disabled={busy} onClick={cancel}>
            取消保留
          </button>
        </div>
      ) : (
        <Link className="cta" to={`/performances/${data.performanceId}/seats`}>回到選位</Link>
      )}

      <p className="page-header__note">付款為模擬流程，<strong>票券無實際入場效力</strong>。</p>
    </section>
  );
}
