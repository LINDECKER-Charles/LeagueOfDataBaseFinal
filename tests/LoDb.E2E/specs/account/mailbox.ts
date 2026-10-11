import { type APIRequestContext, expect } from '@playwright/test';

// The stack's Mailpit (compose.override.yaml); a slot points the suite at its own.
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

// The text of the newest e-mail sent to `address` (Mailpit lists the newest first), if any.
async function newestMail(request: APIRequestContext, address: string): Promise<string | null> {
  const id = await lastMailId(request, address);
  if (id === null) {
    return null;
  }
  const mail = (await (await request.get(`${MAIL_URL}/api/v1/message/${id}`)).json()) as Mail;
  return mail.Text === '' ? mail.HTML.replaceAll('&amp;', '&') : mail.Text;
}

// The path of the first link to the account page `page` in `mail`: query and fragment kept.
function linkIn(mail: string, page: string): string | undefined {
  return new RegExp(`/[\\w-]+/account/${page}[^\\s"'<>)\\]]*`).exec(mail)?.[0];
}

/** The text of the last e-mail sent to `address`. */
export async function lastMailTo(request: APIRequestContext, address: string): Promise<string> {
  let mail: string | null = null;
  await expect
    .poll(async () => (mail = await newestMail(request, address)), {
      message: `an e-mail to ${address}`,
      timeout: DELIVERY_TIMEOUT_MS,
    })
    .not.toBeNull();
  return mail ?? '';
}

/**
 * The path of the first link to an account page in an e-mail, such as `verify-email`. The
 * origin the API writes is the canonical one, not the stack's: only the path is followed,
 * with its query and its fragment, where the token travels.
 */
export function accountLinkIn(mail: string, page: string): string {
  const link = linkIn(mail, page);
  expect(link, `a link to ${page} in the e-mail`).toBeDefined();
  return link ?? '';
}

/**
 * The link to `page` of the newest e-mail sent to `address`, once one holds it: an earlier
 * e-mail to the same address (the confirmation) may still be the newest for a while.
 */
export async function accountLinkMailedTo(
  request: APIRequestContext,
  address: string,
  page: string,
): Promise<string> {
  let link: string | undefined;
  await expect
    .poll(
      async () => {
        const mail = await newestMail(request, address);
        return (link = mail === null ? undefined : linkIn(mail, page));
      },
      { message: `an e-mail to ${address} with a link to ${page}`, timeout: DELIVERY_TIMEOUT_MS },
    )
    .toBeDefined();
  return link ?? '';
}
