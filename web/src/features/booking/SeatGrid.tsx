import './seatGrid.css';
import type { SeatDto, SectionDto } from '../../shared/api/types';
import { Icon } from '../../shared/ui/Icon';

export function SeatGrid({
  section,
  seats,
  selected,
  disabled,
  sport,
  onSelect,
}: {
  section: SectionDto;
  seats: SeatDto[];
  selected: number[];
  disabled: boolean;
  sport: boolean;
  onSelect: (seat: SeatDto) => void;
}) {
  const rows = Array.from({ length: section.rowCount }, (_, i) => i + 1);
  return (
    <div className="seat-grid-panel">
      <div className="seat-grid-panel__heading">
        <div>
          <h2>
            {section.code} 區 · {section.name}
          </h2>
          <p>點選座位後，於選取摘要確認明細</p>
        </div>
        <span className="badge">
          {seats.filter((s) => s.status === 'Available').length} 席可選
        </span>
      </div>
      <ul className="legend">
        <li>
          <span className="seat seat--available" />
          可選
        </li>
        <li>
          <span className="seat seat--selected">✓</span>已選
        </li>
        <li>
          <span className="seat seat--held">−</span>保留中
        </li>
        <li>
          <span className="seat seat--sold">×</span>已售出
        </li>
      </ul>
      <div
        className="seat-scroll"
        tabIndex={0}
        role="region"
        aria-label={`${section.code} 區座位圖，可左右捲動`}
      >
        <div
          className="seat-grid"
          style={{ minWidth: 48 + section.seatsPerRow * 42 }}
        >
          <div className={`stage${sport ? ' stage--sport' : ''}`}>
            <span>{sport ? '比賽場地' : '舞台'}</span>
          </div>
          <div className="seat-grid__direction">
            面向{sport ? '場地' : '舞台'}
            <span>↑</span>
          </div>
          {rows.map((row) => (
            <div className="seat-row" key={row}>
              <span className="row__label">{row} 排</span>
              {seats
                .filter((s) => s.rowNumber === row)
                .sort((a, b) => a.seatNumber - b.seatNumber)
                .map((seat) => {
                  const isSelected = selected.includes(seat.id);
                  const status = isSelected
                    ? '已選取'
                    : seat.status === 'Available'
                      ? '可選'
                      : seat.status === 'Held'
                        ? '保留中'
                        : '已售出';
                  return (
                    <button
                      type="button"
                      key={seat.id}
                      className={`seat seat--${isSelected ? 'selected' : seat.status.toLowerCase()}`}
                      aria-pressed={isSelected}
                      aria-label={`${section.code} 區 第 ${seat.rowNumber} 排 第 ${seat.seatNumber} 席，${status}，${section.price} 元`}
                      disabled={
                        disabled || (seat.status !== 'Available' && !isSelected)
                      }
                      onClick={() => onSelect(seat)}
                    >
                      {isSelected
                        ? '✓'
                        : seat.status === 'Sold'
                          ? '×'
                          : seat.status === 'Held'
                            ? '−'
                            : seat.seatNumber}
                    </button>
                  );
                })}
              <span className="row__label row__label--end">{row}</span>
            </div>
          ))}
        </div>
      </div>
      <p className="seat-grid__hint">
        <Icon name="info" size={14} />
        座位圖可左右滑動。座位狀態會定期更新，成功保留後才為你鎖定。
      </p>
    </div>
  );
}
