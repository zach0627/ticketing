export const seatPollingInterval = 10_000;
export const seatIdleTimeout = 10 * 60_000;

export function shouldPollSeats(lastActivity: number, now: number): boolean {
  return now - lastActivity < seatIdleTimeout;
}
