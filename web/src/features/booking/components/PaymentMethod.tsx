import { Icon } from '../../../shared/ui/Icon';

export function PaymentMethod() {
  return (
    <section className="panel">
      <h2>付款方式</h2>
      <div className="payment-method">
        <Icon name="check" size={22} />
        <div>
          <strong>模擬付款</strong>
          <p>無需提供信用卡資訊，也不會產生實際扣款。</p>
        </div>
      </div>
      <p className="payment-notice" style={{ marginTop: 18, marginBottom: 0 }}>
        活動、演出者與場館皆為虛構。完成流程後，可在「我的訂單」查看票券。
      </p>
    </section>
  );
}
