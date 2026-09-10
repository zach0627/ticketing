import './seatSelection.css';
import { useMemo, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';
import { useSeatMap } from './useSeatMap';
import { useReservation } from './useReservation';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import { BookingSteps } from '../../shared/ui/BookingSteps';
import { Icon } from '../../shared/ui/Icon';
import type { SeatDto } from '../../shared/api/types';
import { describeSalesStatus, formatPrice } from '../../shared/utils/format';
import { SeatPicker } from './SeatPicker';
import {
  readSelectionDraft,
  selectedTotal,
  toggleSelection,
} from './selection';

export function SeatSelectionPage() {
  const { performanceId = '' } = useParams();
  const id = Number(performanceId);
  const location = useLocation();
  const [draft] = useState(() =>
    readSelectionDraft(
      (location.state as { seatDraft?: unknown } | null)?.seatDraft,
      id,
    ),
  );
  const [selected, setSelected] = useState<number[]>(draft?.seatIds ?? []);
  const [sectionId, setSectionId] = useState<number | null>(
    draft?.sectionId ?? null,
  );
  const [contiguous, setContiguous] = useState(
    draft?.selectionMode === 'Contiguous',
  );
  const [quantity, setQuantity] = useState(draft?.quantity ?? 2);
  const { data, isPending, error: loadError, refetch } = useSeatMap(id);

  const { user, error, setError, isSubmitting, uncertain, reserve } =
    useReservation(id, refetch, () => setSelected([]));

  const seatsById = useMemo(
    () => new Map(data?.seats.map((seat) => [seat.id, seat])),
    [data],
  );
  if (isPending) return <Spinner />;
  if (loadError)
    return <ErrorMessage error={loadError} onRetry={() => void refetch()} />;
  const section =
    data.sections.find((s) => s.id === sectionId) ?? data.sections[0];
  const onSale = data.salesStatus === 'OnSale';
  const wanted = contiguous ? quantity : selected.length;
  const total = contiguous
    ? section.price * quantity
    : selectedTotal(selected, data.seats, data.sections);
  const staleSelection = selected.some(
    (seatId) => seatsById.get(seatId)?.status !== 'Available',
  );
  const locked = isSubmitting || uncertain;

  function toggleSeat(seat: SeatDto) {
    if (locked || contiguous || !onSale) return;
    if (
      !selected.includes(seat.id) &&
      selected.length >= data!.maxTicketsPerBuyer
    )
      setError(
        `本場每人最多 ${data!.maxTicketsPerBuyer} 張，可先取消已選座位再更換。`,
      );
    else setError(null);
    setSelected((current) =>
      toggleSelection(current, seat, data!.seats, data!.maxTicketsPerBuyer),
    );
  }

  function submit() {
    if (!uncertain && (!onSale || !wanted || staleSelection)) return;
    void reserve({
      sectionId: section.id,
      quantity: wanted,
      selectionMode: contiguous ? 'Contiguous' : 'Manual',
      seatIds: contiguous ? [] : selected,
    });
  }

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
          onSectionChange={(id) => {
            setSectionId(id);
            setSelected([]);
            setError(null);
          }}
          onModeChange={(value) => {
            setContiguous(value);
            setSelected([]);
            setError(null);
          }}
          onQuantityChange={setQuantity}
          onSelect={toggleSeat}
        />
        <aside className="selection-summary" aria-label="選取摘要">
          <div className="selection-summary__title">
            <Icon name="ticket" size={21} />
            <h2>你的座位</h2>
            <span>{wanted} 張</span>
          </div>
          {wanted > 0 ? (
            <ul className="selected-seats">
              {contiguous ? (
                <li>
                  <div>
                    <strong>{section.code} 區 · 自動連號</strong>
                    <small>同排連續 {quantity} 席，保留後確認座號</small>
                  </div>
                  <b>{formatPrice(total)}</b>
                </li>
              ) : (
                selected.map((seatId) => {
                  const seat = seatsById.get(seatId);
                  if (!seat) return null;
                  return (
                    <li key={seatId}>
                      <div>
                        <strong>
                          {section.code} 區 · {seat.rowNumber} 排{' '}
                          {seat.seatNumber} 號
                        </strong>
                        <small>{section.name}</small>
                      </div>
                      <b>{formatPrice(section.price)}</b>
                      <button
                        className="icon-button"
                        type="button"
                        disabled={locked}
                        aria-label={`移除 ${seat.rowNumber} 排 ${seat.seatNumber} 號`}
                        onClick={() =>
                          setSelected((current) =>
                            current.filter((s) => s !== seatId),
                          )
                        }
                      >
                        <Icon name="close" size={13} />
                      </button>
                    </li>
                  );
                })
              )}
            </ul>
          ) : (
            <div className="selection-summary__empty">
              <Icon name="ticket" size={32} />
              <p>好位子，等你選。</p>
              <small>請在座位圖點選座位</small>
            </div>
          )}
          <div className="selection-total">
            <span>票券總額</span>
            <strong>{formatPrice(total)}</strong>
          </div>
          <p className="selection-summary__note">
            <Icon name="clock" size={16} />
            成功保留後，請於五分鐘內完成付款。
          </p>
          {staleSelection && !uncertain && (
            <p className="auth-form__error" role="alert">
              部分座位已被其他人保留，請取消選取後重新選位。
            </p>
          )}
          {error && (
            <p className="auth-form__error" role="alert">
              {error}
            </p>
          )}
          <button
            className="cta reservation-submit"
            type="button"
            disabled={
              isSubmitting ||
              (!uncertain && (!onSale || wanted === 0 || staleSelection))
            }
            onClick={submit}
          >
            {isSubmitting
              ? '正在確認…'
              : uncertain
                ? '確認保留結果'
                : user
                  ? '確認保留'
                  : '登入後保留'}
            <Icon name="arrow" size={17} />
          </button>
          <Link className="selection-help" to="/guide">
            購票流程與常見問題
            <Icon name="chevron" size={13} />
          </Link>
        </aside>
      </div>
      <div className="mobile-booking-bar">
        <div>
          <small>已選 {wanted} 張</small>
          <strong>{formatPrice(total)}</strong>
        </div>
        <button
          className="cta"
          type="button"
          disabled={
            isSubmitting ||
            (!uncertain && (!onSale || wanted === 0 || staleSelection))
          }
          onClick={submit}
        >
          {isSubmitting
            ? '正在確認…'
            : uncertain
              ? '確認保留結果'
              : user
                ? '確認保留'
                : '登入後保留'}
          <Icon name="arrow" size={17} />
        </button>
      </div>
    </article>
  );
}
