const salesStatusText: Record<string, string> = {
  OnSale: '售票中',
  NotYetOnSale: '尚未開賣',
  SalesClosed: '已停售',
  Paused: '暫停售票',
};

export const describeSalesStatus = (status: string): string =>
  salesStatusText[status] ?? status;
