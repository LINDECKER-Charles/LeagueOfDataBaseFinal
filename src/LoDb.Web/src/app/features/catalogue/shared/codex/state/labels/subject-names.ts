import type { ChangeSubject } from '../../../../../../core/api/generated/models/change-subject';

/** The name of a spell, a rune or a skin of the page's entity, null when it holds none. */
export type SubjectNames = (subject: ChangeSubject) => string | null;
