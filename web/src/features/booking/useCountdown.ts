import { useEffect, useState } from 'react';

/**
 * 保留剩餘秒數。
 *
 * 三個刻意的決定：
 *
 * ① **基準是伺服器時間**。用 `expiresAtUtc − serverNowUtc` 算出「還有幾秒」，
 *    而不是 `expiresAtUtc − 瀏覽器現在時間`——使用者的電腦時鐘可能差好幾分鐘，
 *    那會讓倒數顯示一個完全錯誤的數字（設計文件 13 第 4 節）。
 *
 * ② **遞減用 `performance.now()`**，不是每秒再讀一次 `Date.now()`。
 *    它是單調時鐘，不會因為系統校時或使用者手動改時間而跳動。
 *
 * ③ 只有計時器的 callback 會更新 state，effect 本身不同步設值。
 *    重新抓到資料時最多有 250ms 顯示舊秒數——倒數看不出來，
 *    但換來的是「沒有 render 期間的副作用」這個乾淨的性質。
 *
 * 倒數歸零只是畫面提示。**真正的裁決永遠在伺服器**：
 * 它會依自己的時間判斷這筆保留過期了沒有。
 */
export function useCountdown(serverNowUtc: string, expiresAtUtc: string): number {
  const totalSeconds = Math.max(
    0,
    Math.floor((Date.parse(expiresAtUtc) - Date.parse(serverNowUtc)) / 1000),
  );

  const [remaining, setRemaining] = useState(totalSeconds);

  useEffect(() => {
    const startedAt = performance.now();

    const update = () => {
      const elapsed = Math.floor((performance.now() - startedAt) / 1000);
      setRemaining(Math.max(0, totalSeconds - elapsed));
    };

    const timer = window.setInterval(update, 250);
    return () => window.clearInterval(timer);
  }, [totalSeconds]);

  return remaining;
}

export function formatRemaining(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}
