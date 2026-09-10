import './hold.css';
import { useEffect, useRef } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { bookingApi } from './api';
import { catalogApi } from '../catalog/api';
import { formatRemaining, useCountdown } from './useCountdown';
import { useCheckout } from './useCheckout';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { BookingSteps } from '../../shared/ui/BookingSteps';
import { EventImage } from '../../shared/ui/EventImage';
import { Icon } from '../../shared/ui/Icon';
import { formatPrice, formatTaipei } from '../../shared/utils/format';

export function HoldPage() {
  const { holdId = '' } = useParams();
  const {
    data,
    isPending,
    error: loadError,
    refetch,
  } = useQuery({
    queryKey: ['hold', holdId],
    queryFn: ({ signal }) => bookingApi.getHold(holdId, signal),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
  const { error, busy, uncertain, pay, cancel } = useCheckout(
    holdId,
    data?.performanceId,
    refetch,
  );
  const seatmap = useQuery({
    queryKey: ['seatmap', data?.performanceId],
    queryFn: ({ signal }) => catalogApi.getSeatMap(data!.performanceId, signal),
    enabled: !!data,
  });
  const event = useQuery({
    queryKey: ['event', seatmap.data?.eventCode],
    queryFn: ({ signal }) =>
      catalogApi.getEvent(seatmap.data!.eventCode, signal),
    enabled: !!seatmap.data,
  });
  const remaining = useCountdown(
    data?.serverNowUtc ?? '',
    data?.expiresAtUtc ?? '',
  );
  const expiryChecked = useRef(false);
  useEffect(() => {
    if (
      data?.status === 'Active' &&
      remaining === 0 &&
      !expiryChecked.current
    ) {
      expiryChecked.current = true;
      void refetch();
    }
  }, [data?.status, remaining, refetch]);

  if (isPending) return <Spinner />;
  if (loadError)
    return <ErrorMessage error={loadError} onRetry={() => void refetch()} />;
  if (data.status !== 'Active') {
    const completed = data.status === 'Completed' && data.orderId;
    return (
      <section className="hold">
        <BookingSteps current={completed ? 3 : 2} />
        <div className="hold-status-panel">
          <Icon name={completed ? 'check' : 'clock'} size={40} />
          <h1>
            {completed
              ? '購票已完成，票券準備好了。'
              : data.status === 'Expired'
                ? '這次座位保留已到期'
                : '已取消座位保留'}
          </h1>
          <p>
            {completed
              ? '前往訂單查看你的活動與座位明細。'
              : '座位已重新開放，可以回到座位圖再次選擇。'}
          </p>
          <Link
            className="cta"
            to={
              completed
                ? `/orders/${data.orderId}`
                : `/performances/${data.performanceId}/seats`
            }
          >
            {completed ? '查看票券' : '重新選位'}
            <Icon name="arrow" size={17} />
          </Link>
        </div>
      </section>
    );
  }

  return (
    <section className="hold">
      <BookingSteps current={2} />
      <header className="booking-heading">
        <div>
          <h1>確認訂單與付款</h1>
          <p>你的座位已暫時保留，請確認以下資訊後完成付款。</p>
        </div>
      </header>
      <div className={`countdown-banner${remaining < 60 ? ' is-ending' : ''}`}>
        <Icon name="clock" size={26} />
        <div>
          <strong>
            {remaining > 0
              ? '座位保留中，請在時間內完成付款'
              : '保留時間已到，正在確認最新狀態'}
          </strong>
          <p>逾時未完成付款，座位將重新開放。</p>
        </div>
        <time aria-label={`保留剩餘 ${formatRemaining(remaining)}`}>
          {formatRemaining(remaining)}
        </time>
      </div>
      <div className="hold-layout">
        <div className="hold-main">
          {event.data && (
            <section className="panel hold-event">
              <EventImage src={event.data.imagePath} alt={event.data.title} />
              <div>
                <span className="badge">{event.data.genre}</span>
                <h2>{event.data.title}</h2>
                <p>{formatTaipei(event.data.performance.startsAtUtc)}</p>
                <p>
                  {event.data.performance.venue} · {event.data.performance.city}
                </p>
              </div>
            </section>
          )}
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
          <section className="panel">
            <h2>付款方式</h2>
            <div className="payment-method">
              <Icon name="check" size={22} />
              <div>
                <strong>模擬付款</strong>
                <p>無需提供信用卡資訊，也不會產生實際扣款。</p>
              </div>
            </div>
            <p
              className="payment-notice"
              style={{ marginTop: 18, marginBottom: 0 }}
            >
              活動、演出者與場館皆為虛構。完成流程後，可在「我的訂單」查看票券。
            </p>
          </section>
        </div>
        <aside className="hold-receipt">
          <h2>付款明細</h2>
          <div className="hold-receipt__line">
            <span>票券數量</span>
            <span>{data.items.length} 張</span>
          </div>
          <div className="hold-receipt__line">
            <span>票券金額</span>
            <span>{formatPrice(data.totalAmount, data.currency)}</span>
          </div>
          <div className="hold-receipt__total">
            <span>總計</span>
            <strong>{formatPrice(data.totalAmount, data.currency)}</strong>
          </div>
          {error && (
            <p className="auth-form__error" role="alert">
              {error}
            </p>
          )}
          {uncertain && !error && (
            <p className="notice">上次付款的結果尚未確認，請接續確認。</p>
          )}
          <button
            className="cta"
            type="button"
            disabled={busy}
            onClick={() => pay('Succeeded')}
          >
            {busy ? '正在確認…' : uncertain ? '確認付款結果' : '完成模擬付款'}
            <Icon name="arrow" size={17} />
          </button>
          <button
            className="text-button"
            type="button"
            disabled={busy || uncertain}
            onClick={cancel}
          >
            取消保留，重新選位
          </button>
          <p className="page-header__note">
            <Icon name="shield" size={13} /> 票券無實際入場效力
          </p>
          <details className="payment-test">
            <summary>其他付款情境</summary>
            <p>可嘗試付款失敗的處理流程；保留到期前仍可重新付款。</p>
            <button
              className="cta cta--secondary"
              type="button"
              disabled={busy || uncertain}
              onClick={() => pay('Failed')}
            >
              模擬付款失敗
            </button>
          </details>
        </aside>
      </div>
    </section>
  );
}
