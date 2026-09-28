/** A reason the form offers: the code the API takes and the key of its label. */
export interface ContactCategory {
  readonly code: string;
  readonly label: string;
}

/** The reasons of the legacy form, in its order: the first one is picked at first. */
export const CONTACT_CATEGORIES: readonly ContactCategory[] = [
  { code: 'bug', label: 'contact.form.category_bug' },
  { code: 'feedback', label: 'contact.form.category_feedback' },
  { code: 'review', label: 'contact.form.category_review' },
  { code: 'commercial', label: 'contact.form.category_commercial' },
];
