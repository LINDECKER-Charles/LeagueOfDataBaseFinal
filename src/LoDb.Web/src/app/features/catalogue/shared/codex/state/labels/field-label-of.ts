import type { ChangedField } from '../../../../../../core/api/generated/models/changed-field';
import type { Translate } from '../../texts/translate';
import type { SubjectNames } from './subject-names';
import { subjectLabelOf } from './subject-label-of';

/** Root catalogue prefix of the labels, one per field template (`ChangeField`). */
const FIELD_LABELS = 'detail.field';

/**
 * The label of a changed field in the page's locale: its template's, the subject woven in
 * ("Orb of Deception (Q): cooldown", "Attack damage per level").
 */
export function fieldLabelOf(
  field: Pick<ChangedField, 'field' | 'subject'>,
  translate: Translate,
  names?: SubjectNames,
): string {
  const subject = field.subject === null ? '' : subjectLabelOf(field.subject, translate, names);
  return translate(`${FIELD_LABELS}.${field.field}`, { subject });
}
