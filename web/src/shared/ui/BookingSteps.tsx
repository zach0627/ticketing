import './bookingSteps.css';
import { Icon } from './Icon';

export function BookingSteps({ current }: { current: 1 | 2 | 3 }) {
  return (
    <ol className="booking-steps" aria-label="購票進度">
      {['選擇座位', '確認與付款', '取得票券'].map((label, i) => (
        <li
          key={label}
          className={
            i + 1 === current
              ? 'is-current'
              : i + 1 < current
                ? 'is-complete'
                : ''
          }
          aria-current={i + 1 === current ? 'step' : undefined}
        >
          <span className="booking-steps__number">
            {i + 1 < current ? <Icon name="check" size={15} /> : `0${i + 1}`}
          </span>
          <span>{label}</span>
        </li>
      ))}
    </ol>
  );
}
