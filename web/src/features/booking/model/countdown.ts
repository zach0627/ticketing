/** 使用伺服器傳回的時間差，避免使用者裝置的時鐘影響保留提示。 */
export function remainingSeconds(
  serverNowUtc: string,
  expiresAtUtc: string,
  elapsedMs = 0,
): number {
  const duration = Date.parse(expiresAtUtc) - Date.parse(serverNowUtc);
  if (!Number.isFinite(duration)) return 0;
  return Math.max(0, Math.ceil((duration - Math.max(0, elapsedMs)) / 1000));
}

export function formatRemaining(seconds: number): string {
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}
