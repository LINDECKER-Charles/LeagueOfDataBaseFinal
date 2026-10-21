import type { AdminText } from '../../../../../shared/inject-admin-text';
import { relativeTime } from './relative-time';

const TEXTS: AdminText = {
  text: (key, params) =>
    `${key.replace('prerender.common.time.', '')} ${params?.['duration'] ?? ''}`.trim(),
  term: (_group, name) => name,
};
const NOW = 1_000_000;

describe('relativeTime', () => {
  it('dates a past instant from now', () => {
    expect(relativeTime(NOW - 4_000, NOW, TEXTS)).toBe('ago 4 s');
    expect(relativeTime(NOW - 125_000, NOW, TEXTS)).toBe('ago 2 min');
  });

  it('dates a future instant from now', () => {
    expect(relativeTime(NOW + 18_000, NOW, TEXTS)).toBe('in 18 s');
  });

  it('reads an instant within a second as now', () => {
    expect(relativeTime(NOW - 999, NOW, TEXTS)).toBe('now');
    expect(relativeTime(NOW + 500, NOW, TEXTS)).toBe('now');
  });
});
