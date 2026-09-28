import type { Locale } from '../../../../core/i18n/locales';
import type { EditorView } from '../editor-view';

/** What a route of the editor asks for, read from its URL. */
export interface EntryRequest {
  readonly view: Exclude<EditorView, 'mine'>;
  /** The build edited or imported; null for a new one. */
  readonly id: number | null;
  readonly locale: Locale;
  /** The URL requested, whose `?version=` and `?lang=` set the context. */
  readonly url: string;
  /** The patch an import targets (`?to=`); null for the latest. */
  readonly to: string | null;
}
