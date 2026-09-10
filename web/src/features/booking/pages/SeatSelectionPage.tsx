import '../styles/seatSelection.css';
import { Link } from 'react-router';
import { BookingSteps } from '../../../shared/ui/BookingSteps';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { formatPrice } from '../../../shared/utils/format';
import { describeSalesStatus } from '../../catalog';
import { ReservationButton } from '../components/ReservationButton';
import { SeatPicker } from '../components/SeatPicker';
import { SelectionSummary } from '../components/SelectionSummary';
import { useSeatSelection } from '../hooks/useSeatSelection';

export function SeatSelectionPage() {
  const {
    query,
    section,
    onSale,
    wanted,
    total,
    staleSelection,
    locked,
    selected,
    seatsById,
    contiguous,
    quantity,
    user,
    error,
    isSubmitting,
    uncertain,
    changeSection,
    changeMode,
    setQuantity,
    toggleSeat,
    removeSeat,
    submit,
  } = useSeatSelection();
  const { data, isPending, error: loadError, refetch } = query;
  if (isPending) return <Spinner />;
  if (loadError)
    return <ErrorMessage error={loadError} onRetry={() => void refetch()} />;
  if (!section) return <Spinner />;
  return (
    <article className="seatmap">
      <Link className="back-link" to={`/events/${data.eventCode}`}>
        ← 返回活動資訊
      </Link>
      <BookingSteps current={1} />
      <header className="booking-heading">
        <div>
          <h1>{data.eventTitle}</h1>
          <p>請選擇票區與座位，再確認保留。</p>
        </div>
        <span className={`status status--${data.salesStatus.toLowerCase()}`}>
          {describeSalesStatus(data.salesStatus)}
        </span>
      </header>
      {!onSale && (
        <p className="notice">
          {describeSalesStatus(data.salesStatus)}，目前無法新增保留。
        </p>
      )}
      <div className="booking-layout">
        <SeatPicker
          data={data}
          section={section}
          selected={selected}
          contiguous={contiguous}
          quantity={quantity}
          locked={locked}
          onSale={onSale}
          onSectionChange={changeSection}
          onModeChange={changeMode}
          onQuantityChange={setQuantity}
          onSelect={toggleSeat}
        />
        <SelectionSummary
          section={section}
          selectedSeats={selected.flatMap((id) => seatsById.get(id) ?? [])}
          contiguous={contiguous}
          quantity={quantity}
          wanted={wanted}
          total={total}
          locked={locked}
          staleSelection={staleSelection}
          uncertain={uncertain}
          error={error}
          onRemove={removeSeat}
        >
          <ReservationButton
            className="cta reservation-submit"
            isSubmitting={isSubmitting}
            uncertain={uncertain}
            onSale={onSale}
            wanted={wanted}
            staleSelection={staleSelection}
            isAuthenticated={!!user}
            onSubmit={submit}
          />
        </SelectionSummary>
      </div>
      <div className="mobile-booking-bar">
        <div>
          <small>已選 {wanted} 張</small>
          <strong>{formatPrice(total)}</strong>
        </div>
        <ReservationButton
          className="cta"
          isSubmitting={isSubmitting}
          uncertain={uncertain}
          onSale={onSale}
          wanted={wanted}
          staleSelection={staleSelection}
          isAuthenticated={!!user}
          onSubmit={submit}
        />
      </div>
    </article>
  );
}
