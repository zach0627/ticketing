import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { formatPrice } from '../../../shared/utils/format';
import type { SeatDto } from '../../catalog';

import type { ReactNode } from 'react';
import type { SectionDto } from '../../catalog';

export function SelectionSummary({
  section,
  selectedSeats,
  contiguous,
  quantity,
  wanted,
  total,
  locked,
  staleSelection,
  uncertain,
  error,
  onRemove,
  children,
}: {
  section: SectionDto;
  selectedSeats: SeatDto[];
  contiguous: boolean;
  quantity: number;
  wanted: number;
  total: number;
  locked: boolean;
  staleSelection: boolean;
  uncertain: boolean;
  error: string | null;
  onRemove: (seatId: number) => void;
  children: ReactNode;
}) {
  return (
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
            selectedSeats.map((seat) => {
              const seatId = seat.id;
              return (
                <li key={seatId}>
                  <div>
                    <strong>
                      {section.code} 區 · {seat.rowNumber} 排 {seat.seatNumber}{' '}
                      號
                    </strong>
                    <small>{section.name}</small>
                  </div>
                  <b>{formatPrice(section.price)}</b>
                  <button
                    className="icon-button"
                    type="button"
                    disabled={locked}
                    aria-label={`移除 ${seat.rowNumber} 排 ${seat.seatNumber} 號`}
                    onClick={() => onRemove(seatId)}
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
      {children}
      <Link className="selection-help" to="/guide">
        購票流程與常見問題
        <Icon name="chevron" size={13} />
      </Link>
    </aside>
  );
}
