import type { ChangedField } from '../../../../../../core/api/generated/models/changed-field';
import type { Translate } from '../../texts/translate';
import { fieldLabelOf } from './field-label-of';
import type { SubjectNames } from './subject-names';

/** The labels of changed fields, in the API's order, each once. */
export function fieldLabelsOf(
  fields: readonly Pick<ChangedField, 'field' | 'subject'>[],
  translate: Translate,
  names?: SubjectNames,
): string[] {
  return [...new Set(fields.map((field) => fieldLabelOf(field, translate, names)))];
}
