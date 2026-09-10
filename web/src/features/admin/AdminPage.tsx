import './admin.css';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApi } from './api';
import { ApiError } from '../../shared/api/http';
import { newIdempotencyKey } from '../../shared/api/idempotency';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import {
  describeSalesStatus,
  formatPrice,
  formatTaipei,
} from '../../shared/utils/format';

export function AdminPage() {
  const queryClient = useQueryClient();
  const [performanceId, setPerformanceId] = useState<number | null>(null);
  const [confirmation, setConfirmation] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const dashboard = useQuery({
    queryKey: ['admin', 'dashboard'],
    queryFn: ({ signal }) => adminApi.dashboard(signal),
  });

  const orders = useQuery({
    queryKey: ['admin', 'orders', performanceId],
    queryFn: ({ signal }) => adminApi.orders(performanceId, signal),
  });

  function refresh() {
    return queryClient.invalidateQueries({ queryKey: ['admin'] });
  }

  const togglePause = useMutation({
    mutationFn: ({ id, paused }: { id: number; paused: boolean }) =>
      adminApi.setSalesPaused(id, paused, newIdempotencyKey()),
    onSuccess: async (result) => {
      setError(null);
      setMessage(
        `場次 ${result.performanceId} 已${result.isSalesPaused ? '暫停' : '恢復'}售票。`,
      );
      await refresh();
    },
    onError: (caught: unknown) =>
      setError(
        caught instanceof ApiError ? caught.message : '操作失敗，請再試一次。',
      ),
  });

  const reset = useMutation({
    mutationFn: () => adminApi.reset(newIdempotencyKey()),
    onSuccess: async (result) => {
      setError(null);
      setConfirmation('');
      setMessage(
        `重置完成：刪除 ${result.ordersDeleted} 張訂單、${result.holdsDeleted} 筆保留，` +
          `釋放 ${result.seatsReleased} 個座位；最早的一場現在是 ${formatTaipei(result.earliestPerformanceUtc)}。`,
      );
      // 重置會刪掉所有購買資料，別人的訂單頁會開始回 404——先把整個快取清掉
      queryClient.clear();
      await refresh();
    },
    onError: (caught: unknown) =>
      setError(
        caught instanceof ApiError ? caught.message : '重置失敗，請再試一次。',
      ),
  });

  if (dashboard.isPending) return <Spinner />;
  if (dashboard.error) return <ErrorMessage error={dashboard.error} />;

  const busy = togglePause.isPending || reset.isPending;

  return (
    <section className="admin-page">
      <h1>管理後台</h1>
      <p className="page-header__note">
        伺服器時間 {formatTaipei(dashboard.data.serverNowUtc)}
      </p>

      {message && (
        <p className="notice" role="status">
          {message}
        </p>
      )}
      {error && (
        <p className="auth-form__error" role="alert">
          {error}
        </p>
      )}

      <dl className="detail__facts">
        <div>
          <dt>有效保留</dt>
          <dd>{dashboard.data.activeHolds}</dd>
        </div>
        <div>
          <dt>已售座位</dt>
          <dd>{dashboard.data.soldSeats}</dd>
        </div>
        <div>
          <dt>訂單</dt>
          <dd>{dashboard.data.orders}</dd>
        </div>
      </dl>

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
            {dashboard.data.performances.map((performance) => (
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
                      togglePause.mutate({
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
                    onClick={() => setPerformanceId(performance.id)}
                  >
                    只看這場
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <h2>
        訂單
        {performanceId && (
          <button
            className="site-header__link-button"
            type="button"
            onClick={() => setPerformanceId(null)}
          >
            （顯示全部）
          </button>
        )}
      </h2>

      {orders.isPending && <Spinner />}
      {orders.error && <ErrorMessage error={orders.error} />}
      {orders.data && (
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
              {orders.data.items.map((order) => (
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

      <h2>重置展示資料</h2>
      <p className="notice">
        會<strong>刪掉所有訂單與保留</strong>
        、把座位全部改回可售、活動日期往後平移並解除暫停。
        帳號、活動與稽核紀錄不會被刪。這是為了重複展示而設計的功能，商用系統不會這樣做。
      </p>

      <div className="hold__actions">
        <input
          aria-label="輸入 RESET 以確認"
          placeholder="輸入 RESET"
          value={confirmation}
          onChange={(e) => setConfirmation(e.target.value)}
          className="auth-form__reset-input"
        />
        <button
          className="cta cta--danger"
          type="button"
          disabled={busy || confirmation !== 'RESET'}
          onClick={() => reset.mutate()}
        >
          {reset.isPending ? '重置中…' : '執行重置'}
        </button>
      </div>
    </section>
  );
}
