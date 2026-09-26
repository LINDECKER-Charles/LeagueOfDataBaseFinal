import type { ImportReport } from '../../../../core/api/generated/models/import-report';
import type { EditorMessage } from '../shared/editor-message';

const IMPORT = 'build.import.';

/**
 * What an import tells its author before they review the build: the patch it landed on,
 * then each thing it could not carry over. An item dropped from two steps is named once.
 */
export function reportNoticesOf(report: ImportReport, version: string): EditorMessage[] {
  const notices: EditorMessage[] = [{ key: `${IMPORT}done`, params: { version } }];
  if (report.championMissing) {
    notices.push({ key: `${IMPORT}champion_missing` });
  }
  if (report.runesReset) {
    notices.push({ key: `${IMPORT}runes_reset` });
  }
  if (report.droppedItems.length > 0) {
    const names = new Map(report.droppedItems.map((item) => [item.id, item.name]));
    notices.push({
      key: `${IMPORT}items_dropped`,
      params: { items: [...names.values()].join(', ') },
    });
  }
  return notices;
}
