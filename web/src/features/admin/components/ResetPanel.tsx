export function ResetPanel({
  confirmation,
  onConfirmationChange,
  busy,
  isResetting,
  onReset,
}: {
  confirmation: string;
  onConfirmationChange: (value: string) => void;
  busy: boolean;
  isResetting: boolean;
  onReset: () => void;
}) {
  return (
    <>
      <h2>重置展示資料</h2>
      <p className="notice">
        會<strong>刪掉所有訂單與保留</strong>
        、把座位全部改回可售、活動日期往後平移並解除暫停。
        帳號、活動與稽核紀錄不會被刪。這是為了重複展示而設計的功能，商用系統不會這樣做。
      </p>

      <div className="hold__actions">
        <input
          aria-label="輸入 RESET 以確認"
          placeholder="輸入 RESET"
          value={confirmation}
          onChange={(e) => onConfirmationChange(e.target.value)}
          className="auth-form__reset-input"
        />
        <button
          className="cta cta--danger"
          type="button"
          disabled={busy || confirmation !== 'RESET'}
          onClick={onReset}
        >
          {isResetting ? '重置中…' : '執行重置'}
        </button>
      </div>
    </>
  );
}
