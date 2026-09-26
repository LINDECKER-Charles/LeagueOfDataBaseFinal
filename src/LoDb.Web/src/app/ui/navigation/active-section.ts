/**
 * Section the nav marks as current: the first one, in document order, that crosses the
 * reading band. When none does (the reader sits between two sections), the previous answer
 * holds, so the mark never blinks off mid-scroll.
 */
export function activeSection(
  order: readonly string[],
  crossing: ReadonlySet<string>,
  previous: string | undefined,
): string | undefined {
  return order.find((id) => crossing.has(id)) ?? previous;
}
