const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/**
 * Writes a length of time in seconds as the French admin reads an uptime: its two largest
 * units, `3 j 4 h`, `4 h 12 min`, `12 min`, the seconds dropped past the first minute.
 */
export function duration(seconds: number): string {
  const whole = Math.max(0, Math.floor(seconds));
  if (whole < MINUTE) {
    return `${whole} s`;
  }
  const days = Math.floor(whole / DAY);
  const hours = Math.floor((whole % DAY) / HOUR);
  const minutes = Math.floor((whole % HOUR) / MINUTE);
  if (days > 0) {
    return `${days} j ${hours} h`;
  }
  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}
