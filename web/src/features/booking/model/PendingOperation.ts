export interface PendingOperation {
  /** 綁定使用者：換帳號登入時**不能**恢復別人的操作。 */
  userId: string;
  operation: 'hold' | 'checkout';
  /** 場次 id 或保留 id。 */
  target: string;
  key: string;
  payload?: unknown;
}
