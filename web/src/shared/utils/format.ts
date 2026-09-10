/** 後端一律存 UTC，畫面顯示台北時間（設計文件 04 第 7 節）。 */
export function formatTaipei(utcIso: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    timeZone: 'Asia/Taipei',
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(utcIso));
}

export function formatTaipeiDate(utcIso: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    weekday: 'short',
  }).format(new Date(utcIso));
}

export function formatPrice(amount: number, currency = 'TWD'): string {
  return new Intl.NumberFormat('zh-TW', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  }).format(amount);
}

const salesStatusText: Record<string, string> = {
  OnSale: '售票中',
  NotYetOnSale: '尚未開賣',
  SalesClosed: '已停售',
  Paused: '暫停售票',
};

export const describeSalesStatus = (status: string): string =>
  salesStatusText[status] ?? status;

export function formatCalendarDate(utcIso: string): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(utcIso));
}
