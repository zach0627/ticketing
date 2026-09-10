import { useEffect, useState } from 'react';
import { remainingSeconds } from '../model/countdown';
export { formatRemaining } from '../model/countdown';

/** 倒數只是提示；保留是否過期仍由伺服器裁決。 */
export function useCountdown(
  serverNowUtc: string,
  expiresAtUtc: string,
): number {
  const [tick, setTick] = useState({
    serverNowUtc,
    expiresAtUtc,
    elapsedMs: 0,
  });
  useEffect(() => {
    const startedAt = performance.now();
    const timer = window.setInterval(() => {
      setTick({
        serverNowUtc,
        expiresAtUtc,
        elapsedMs: performance.now() - startedAt,
      });
    }, 250);
    return () => window.clearInterval(timer);
  }, [serverNowUtc, expiresAtUtc]);
  const elapsed =
    tick.serverNowUtc === serverNowUtc && tick.expiresAtUtc === expiresAtUtc
      ? tick.elapsedMs
      : 0;
  return remainingSeconds(serverNowUtc, expiresAtUtc, elapsed);
}
