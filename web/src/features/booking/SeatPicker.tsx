import { FieldSelect } from '../../shared/ui/FieldSelect';
import type { SeatMapDto, SeatDto, SectionDto } from '../../shared/api/types';
import { formatPrice } from '../../shared/utils/format';
import { SeatGrid } from './SeatGrid';

export function SeatPicker({
  data,
  section,
  selected,
  contiguous,
  quantity,
  locked,
  onSale,
  onSectionChange,
  onModeChange,
  onQuantityChange,
  onSelect,
}: {
  data: SeatMapDto;
  section: SectionDto;
  selected: number[];
  contiguous: boolean;
  quantity: number;
  locked: boolean;
  onSale: boolean;
  onSectionChange: (id: number) => void;
  onModeChange: (value: boolean) => void;
  onQuantityChange: (quantity: number) => void;
  onSelect: (seat: SeatDto) => void;
}) {
  return (
    <div className="booking-main">
      <section className="zone-picker">
        <div className="subsection-heading">
          <h2>
            <span>01</span>選擇票區
          </h2>
          <small>
            每筆保留限同一票區 · 每人最多 {data.maxTicketsPerBuyer} 張
          </small>
        </div>
        <div className="zone-options">
          {data.sections.map((zone) => (
            <button
              type="button"
              key={zone.id}
              aria-pressed={zone.id === section.id}
              disabled={locked}
              className={`zone-option${zone.id === section.id ? ' is-active' : ''}`}
              onClick={() => {
                onSectionChange(zone.id);
              }}
            >
              <span className="zone-option__code">{zone.code}</span>
              <span>
                <strong>{zone.name}</strong>
                <small>
                  {
                    data.seats.filter(
                      (s) =>
                        s.sectionId === zone.id && s.status === 'Available',
                    ).length
                  }{' '}
                  席可選
                </small>
              </span>
              <b>{formatPrice(zone.price)}</b>
            </button>
          ))}
        </div>
      </section>
      <section>
        <div className="subsection-heading">
          <h2>
            <span>02</span>選擇座位
          </h2>
          {data.allowsContiguousAllocation && (
            <div className="mode-switch">
              <button
                type="button"
                aria-pressed={!contiguous}
                disabled={locked}
                className={!contiguous ? 'is-active' : ''}
                onClick={() => {
                  onModeChange(false);
                }}
              >
                自行選位
              </button>
              <button
                type="button"
                aria-pressed={contiguous}
                disabled={locked}
                className={contiguous ? 'is-active' : ''}
                onClick={() => {
                  onModeChange(true);
                }}
              >
                自動連號
              </button>
            </div>
          )}
        </div>
        {contiguous && (
          <div className="allocation-options">
            <div>
              <strong>和朋友坐在一起</strong>
              <p>系統會在「{section.code} 區」安排同排連續座位。</p>
            </div>
            <FieldSelect
              label="購票張數"
              value={String(quantity)}
              disabled={locked}
              onChange={(value) => onQuantityChange(Number(value))}
              options={Array.from(
                { length: data.maxTicketsPerBuyer },
                (_, i) => ({ value: String(i + 1), label: `${i + 1} 張` }),
              )}
            />
          </div>
        )}
        <SeatGrid
          section={section}
          seats={data.seats.filter((s) => s.sectionId === section.id)}
          selected={selected}
          disabled={locked || contiguous || !onSale}
          sport={data.category === 'Sport'}
          onSelect={onSelect}
        />
      </section>
    </div>
  );
}
