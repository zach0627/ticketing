import '../styles/hold.css';
import { BookingSteps } from '../../../shared/ui/BookingSteps';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { HoldCountdown } from '../components/HoldCountdown';
import { HoldEvent } from '../components/HoldEvent';
import { HoldReceipt } from '../components/HoldReceipt';
import { HoldSeats } from '../components/HoldSeats';
import { HoldStatus } from '../components/HoldStatus';
import { PaymentMethod } from '../components/PaymentMethod';
import { useHoldDetails } from '../hooks/useHoldDetails';

export function HoldPage() {
  const { query, error, busy, uncertain, pay, cancel, event, remaining } =
    useHoldDetails();
  const { data, isPending, error: loadError, refetch } = query;
  if (isPending) return <Spinner />;
  if (loadError)
    return <ErrorMessage error={loadError} onRetry={() => void refetch()} />;
  if (data.status !== 'Active') return <HoldStatus data={data} />;

  return (
    <section className="hold">
      <BookingSteps current={2} />
      <header className="booking-heading">
        <div>
          <h1>確認訂單與付款</h1>
          <p>你的座位已暫時保留，請確認以下資訊後完成付款。</p>
        </div>
      </header>
      <HoldCountdown remaining={remaining} />
      <div className="hold-layout">
        <div className="hold-main">
          <HoldEvent event={event} />
          <HoldSeats data={data} />
          <PaymentMethod />
        </div>
        <HoldReceipt
          data={data}
          error={error}
          busy={busy}
          uncertain={uncertain}
          pay={pay}
          cancel={cancel}
        />
      </div>
    </section>
  );
}
