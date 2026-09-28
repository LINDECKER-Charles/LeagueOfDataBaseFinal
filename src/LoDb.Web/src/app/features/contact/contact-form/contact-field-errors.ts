/** Key of the message shown under each refused field, by field name. */
export type ContactFieldErrors = Readonly<Record<string, string>>;

// A field or a code the texts do not name reads as the form's generic error.
const GENERIC = 'contact.flash.error';

// The legacy texts: one per field, but three for the message.
const BY_FIELD: Readonly<Record<string, string>> = {
  category: 'contact.error.category',
  email: 'contact.error.email',
};
const BY_MESSAGE_CODE: Readonly<Record<string, string>> = {
  required: 'contact.error.message',
  'too-short': 'contact.error.message_short',
  'too-long': 'contact.error.message_long',
};

function keyOf(field: string, code: string): string {
  if (field === 'message') {
    return BY_MESSAGE_CODE[code] ?? GENERIC;
  }
  return BY_FIELD[field] ?? GENERIC;
}

/** The message of the first code of each refused field. */
export function contactFieldErrors(
  errors: Readonly<Record<string, readonly string[]>>,
): ContactFieldErrors {
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([, codes]) => codes.length > 0)
      .map(([field, codes]) => [field, keyOf(field, codes[0] ?? '')]),
  );
}
