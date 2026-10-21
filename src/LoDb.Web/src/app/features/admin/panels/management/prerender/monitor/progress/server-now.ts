/**
 * The server's clock now, in milliseconds (§ 4.3): the instant of its last answer moved on by
 * the time the client counted since it received it. Every relative time of the monitor
 * (« dernière page il y a 2 s ») measures against it and the server's own stamps, so that no
 * skew between the operator's machine and the host ever shows.
 */
export function serverNow(generatedAt: string, receivedAt: number, now: number): number {
  return Date.parse(generatedAt) + (now - receivedAt);
}
