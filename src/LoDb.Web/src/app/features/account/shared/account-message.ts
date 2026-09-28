import type { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from '../../../core/i18n/locales';
import { ACCOUNT_SCOPE } from './account-scope';

/**
 * A text of the account scope said from outside the account pages (header menu, banner),
 * which never load the scope for their own chrome: it is loaded on demand, then translated.
 */
export async function accountMessage(
  transloco: TranslocoService,
  locale: Locale,
  key: string,
): Promise<string> {
  await firstValueFrom(transloco.load(`${ACCOUNT_SCOPE}/${locale}`)).catch(() => undefined);
  return transloco.translate(key, {}, locale);
}
