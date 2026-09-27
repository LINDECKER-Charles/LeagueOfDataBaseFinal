import type { StorageReport } from '../../../../core/api/generated/models/storage-report';

function row(name: string, bytes: number) {
  return { name, bytes, objects: 10, pct: 50 };
}

const REPORT: StorageReport = {
  generatedAt: '2026-09-27T10:00:00Z',
  ok: true,
  bytes: 6 * 1024 ** 3,
  objects: 180_000,
  timeline: [
    { date: '2026-09-26', bytes: 1024 ** 2, cumulativeBytes: 5 * 1024 ** 3, objects: 20 },
    { date: '2026-09-27', bytes: 2 * 1024 ** 2, cumulativeBytes: 6 * 1024 ** 3, objects: 30 },
  ],
  families: [row('data', 4 * 1024 ** 3), row('img', 2 * 1024 ** 3)],
  blobs: {
    byExt: [row('png', 1024 ** 3)],
    sources: 9_000,
    sourceBytes: 1024 ** 3,
    webpSiblings: 8_100,
    webpBytes: 300 * 1024 ** 2,
    webpCoverage: 0.9,
  },
  data: {
    byType: [row('champion', 2 * 1024 ** 3)],
    byLang: [row('fr_FR', 1024 ** 3)],
    byVersion: [row('16.19.1', 1024 ** 3)],
  },
  dedup: { logicalRefs: 360_000, physicalBlobs: 180_000, ratio: 2, savedBytesApprox: 1024 ** 3 },
  largest: [{ path: 'img/splash/Ahri_0.jpg', bytes: 512 * 1024 }],
  coverage: [
    { version: '16.19.1', objects: 12_000, types: ['champion', 'item'], langs: ['fr_FR'] },
  ],
};

/** A storage report of two days, deduplicated twice over, every ranking filled. */
export function storageReport(changes: Partial<StorageReport> = {}): StorageReport {
  return { ...REPORT, ...changes };
}
