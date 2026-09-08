import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from '../catalog/api';
import { bookingApi } from './api';
import { useAuth } from '../auth/authContext';
import { ApiError } from '../../shared/api/http';
import { newIdempotencyKey, pendingOperations } from '../../shared/api/idempotency';
import { ErrorMessage, Spinner } from '../../shared/ui/States';
import type { SeatDto } from '../../shared/api/types';
import { describeSalesStatus, formatPrice } from '../../shared/utils/format';

function groupByRow(seats: SeatDto[]): [number, SeatDto[]][] {
  const rows = new Map<number, SeatDto[]>();
  for (const seat of seats) {
    const row = rows.get(seat.rowNumber);
    if (row) row.push(seat);
    else rows.set(seat.rowNumber, [seat]);
  }
  return [...rows.entries()].sort(([a], [b]) => a - b);
}

export function SeatSelectionPage() {
  const { performanceId = '' } = useParams();
  const id = Number(performanceId);
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [selected, setSelected] = useState<number[]>([]);
  const [contiguous, setContiguous] = useState(false);
  const [quantity, setQuantity] = useState(2);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { data, isPending, error: loadError, refetch } = useQuery({
    queryKey: ['seatmap', id],
    queryFn: ({ signal }) => catalogApi.getSeatMap(id, signal),
    // 座位圖會被別人改變。停留在這一頁時每 10 秒重抓一次，離開分頁就停
    refetchInterval: 10_000,
    refetchIntervalInBackground: false,
  });

  // 登入者進到這一頁，先問「我在這場是不是已經有保留了」——
  // 有的話直接把他帶回去，不要讓他重複佔位（設計文件 13 第 4 節）
  useEffect(() => {
    if (!user || !Number.isFinite(id)) return;

    const controller = new AbortController();
    bookingApi
      .myHolds(id, controller.signal)
      .then((result) => {
        const existing = result.items[0];
        if (existing) navigate(`/holds/${existing.id}`, { replace: true });
      })
      .catch(() => {
        // 查不到就當作沒有；真正的把關在送出保留時的 409
      });

    return () => controller.abort();
  }, [user, id, navigate]);

  // 重新整理後恢復：上一個還沒確定結果的保留，用**原本的 key** 重送
  useEffect(() => {
    if (!user) return;

    const pending = pendingOperations.read(user.id, 'hold', String(id));
    if (!pending) return;

    bookingApi
      .createHold(id, pending.payload as never, pending.key)
      .then((hold) => {
        pendingOperations.clear();
        navigate(`/holds/${hold.id}`, { replace: true });
      })
      .catch(() => pendingOperations.clear());
  }, [user, id, navigate]);

  const seatsById = useMemo(
    () => new Map(data?.seats.map((seat) => [seat.id, seat])),
    [data],
  );

  const total = useMemo(() => {
    if (!data) return 0;
    return selected.reduce((sum, seatId) => {
      const seat = seatsById.get(seatId);
      const section = data.sections.find((s) => s.id === seat?.sectionId);
      return sum + (section?.price ?? 0);
    }, 0);
  }, [selected, seatsById, data]);

  if (isPending) return <Spinner />;
  if (loadError) return <ErrorMessage error={loadError} />;

  const onSale = data.salesStatus === 'OnSale';
  const canUseContiguous = data.allowsContiguousAllocation;
  const wanted = contiguous ? quantity : selected.length;

  function toggleSeat(seat: SeatDto) {
    if (seat.status !== 'Available' || contiguous) return;

    setSelected((current) =>
      current.includes(seat.id)
        ? current.filter((seatId) => seatId !== seat.id)
        : current.length >= data!.maxTicketsPerBuyer
          ? current
          : [...current, seat.id],
    );
  }

  async function submit() {
    if (!user) {
      // 記住現在在哪一頁，登入完再回來
      navigate('/login', { state: { from: `${location.pathname}${location.search}` } });
      return;
    }

    const section = contiguous
      ? data!.sections[0].id
      : seatsById.get(selected[0])!.sectionId;

    const body = contiguous
      ? { sectionId: section, quantity, selectionMode: 'Contiguous' as const, seatIds: [] }
      : { sectionId: section, quantity: selected.length, selectionMode: 'Manual' as const, seatIds: selected };

    // ⭐ 送出**之前**先記下 key。順序反過來的話，
    // 「送出成功但還沒記下來就斷線」會讓我們永遠不知道該用哪個 key 重送
    const key = newIdempotencyKey();
    pendingOperations.save({ userId: user.id, operation: 'hold', target: String(id), key, payload: body });

    setError(null);
    setIsSubmitting(true);

    try {
      const hold = await bookingApi.createHold(id, body, key);
      pendingOperations.clear();
      navigate(`/holds/${hold.id}`);
    } catch (caught) {
      pendingOperations.clear();

      // 後端在這個錯誤裡附了既有的 holdId，直接把人帶過去，
      // 不要只丟一句「你已經有保留了」讓他自己找
      if (caught instanceof ApiError && caught.code === 'ActiveHoldExists' && caught.holdId) {
        navigate(`/holds/${caught.holdId}`, { replace: true });
        return;
      }

      setError(caught instanceof ApiError ? caught.message : '保留失敗，請再試一次。');
      setSelected([]);
      await refetch();          // 座位圖可能已經變了，重抓一次
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <article className="seatmap">
      <Link className="back-link" to={`/events/${data.eventCode}`}>← 回到活動頁</Link>

      <h1>{data.eventTitle}</h1>
      <p className="seatmap__summary">
        {describeSalesStatus(data.salesStatus)}
        ・剩餘 {data.seats.filter((s) => s.status === 'Available').length} / {data.seats.length} 席
        ・每人最多 {data.maxTicketsPerBuyer} 張
      </p>

      {!onSale && (
        <p className="notice">{describeSalesStatus(data.salesStatus)}，目前無法保留座位。</p>
      )}

      {canUseContiguous && (
        <label className="toggle">
          <input
            type="checkbox"
            checked={contiguous}
            onChange={(e) => {
              setContiguous(e.target.checked);
              setSelected([]);
            }}
          />
          自動連號配位（系統在票區內找第一段同排連續座位）
        </label>
      )}

      {contiguous && (
        <label className="toggle">
          張數
          <select value={quantity} onChange={(e) => setQuantity(Number(e.target.value))}>
            {Array.from({ length: data.maxTicketsPerBuyer }, (_, i) => i + 1).map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
        </label>
      )}

      <ul className="legend">
        <li><span className="seat seat--available" /> 可選</li>
        <li><span className="seat seat--selected" /> 已選</li>
        <li><span className="seat seat--held" /> 保留中</li>
        <li><span className="seat seat--sold" /> 已售出</li>
      </ul>

      {data.sections.map((section) => (
        <section key={section.id} className="zone">
          <h2>
            {section.code} 區・{section.name}
            <span className="zone__price">{formatPrice(section.price)}</span>
          </h2>

          <div className="stage">舞台／場地</div>

          {groupByRow(data.seats.filter((s) => s.sectionId === section.id)).map(([row, seats]) => (
            <div key={row} className="row">
              <span className="row__label">{row} 排</span>
              {seats.map((seat) => {
                const isSelected = selected.includes(seat.id);
                const label =
                  `${section.code} 區 第 ${seat.rowNumber} 排 第 ${seat.seatNumber} 席，` +
                  `${seat.status === 'Available' ? (isSelected ? '已選取' : '可選') : '不可選'}，` +
                  `${section.price} 元`;

                return (
                  <button
                    key={seat.id}
                    type="button"
                    aria-label={label}
                    aria-pressed={isSelected}
                    disabled={seat.status !== 'Available' || contiguous || !onSale}
                    className={`seat seat--${isSelected ? 'selected' : seat.status.toLowerCase()}`}
                    onClick={() => toggleSeat(seat)}
                  >
                    {seat.status === 'Sold' ? '✕' : isSelected ? '✓' : seat.seatNumber}
                  </button>
                );
              })}
            </div>
          ))}
        </section>
      ))}

      <div className="summary-bar">
        <div>
          已選 {wanted} 張
          {!contiguous && total > 0 && <>・合計 {formatPrice(total)}</>}
        </div>

        {error && <p className="auth-form__error" role="alert">{error}</p>}

        <button
          className="cta"
          type="button"
          disabled={!onSale || isSubmitting || wanted === 0}
          onClick={submit}
        >
          {isSubmitting ? '保留中…' : user ? '保留這些座位' : '登入後保留'}
        </button>
      </div>
    </article>
  );
}
