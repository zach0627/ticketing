import { Icon } from '../../../shared/ui/Icon';
import { formatRemaining } from '../hooks/useCountdown';

export function HoldCountdown({ remaining }: { remaining: number }) {
  return (
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
  );
}
