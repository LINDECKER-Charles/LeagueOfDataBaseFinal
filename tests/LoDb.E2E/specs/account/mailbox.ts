import { type APIRequestContext, expect } from '@playwright/test';

// The stack's Mailpit (compose.next.override.yaml); a slot points the suite at its own.
const MAIL_URL = process.env['LODB_E2E_MAIL_URL'] ?? 'http://localhost:18025';
// The API's outbox sends in the background: the e-mail comes within seconds, not at once.
const DELIVERY_TIMEOUT_MS = 30_000;

interface MailSearch {
  readonly messages: readonly { readonly ID: string }[];
}

interface Mail {
  readonly Text: string;
  readonly HTML: string;
}

async function lastMailId(request: APIRequestContext, address: string): Promise<string | null> {
  const search = await request.get(`${MAIL_URL}/api/v1/search`, {
    params: { query: `to:"${address}"` },
  });
  return ((await search.json()) as MailSearch).messages[0]?.ID ?? null;
}

/** The text of the last e-mail sent to `address` (Mailpit lists the newest first). */
export async function lastMailTo(request: APIRequestContext, address: string): Promise<string> {
  let id: string | null = null;
  await expect
    .poll(async () => (id = await lastMailId(request, address)), {
      message: `an e-mail to ${address}`,
      timeout: DELIVERY_TIMEOUT_MS,
    })
    .not.toBeNull();
  const mail = (await (await request.get(`${MAIL_URL}/api/v1/message/${id}`)).json()) as Mail;
  return mail.Text === '' ? mail.HTML.replaceAll('&amp;', '&') : mail.Text;
}

/**
 * The path of the first link to an account page in an e-mail, such as `verify-email`. The
 * origin the API writes is the canonical one, not the stack's: only the path is followed.
 */
export function accountLinkIn(mail: string, page: string): string {
  const link = new RegExp(`/[\\w-]+/account/${page}[^\\s"'<>)\\]]*`).exec(mail)?.[0];
  expect(link, `a link to ${page} in the e-mail`).toBeDefined();
  return link ?? '';
}
