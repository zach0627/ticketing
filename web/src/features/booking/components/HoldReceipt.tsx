import { Icon } from '../../../shared/ui/Icon';
import { formatPrice } from '../../../shared/utils/format';

import type { HoldDto } from '../model/HoldDto';

export function HoldReceipt({
  data,
  error,
  busy,
  uncertain,
  pay,
  cancel,
}: {
  data: HoldDto;
  error: string | null;
  busy: boolean;
  uncertain: boolean;
  pay: (outcome: 'Succeeded' | 'Failed') => Promise<void>;
  cancel: () => Promise<void>;
}) {
  return (
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
  );
}
