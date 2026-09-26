import type { GameMode } from '../../../../core/api/generated/models/game-mode';
import type { TrendsPage } from '../../../../core/api/generated/models/trends-page';
import type { ListTrends$Params } from '../../../../core/api/generated/fn/trends/list-trends';
import type { Locale } from '../../../../core/i18n/locales';

/** What the route of the trends resolves: a page of them, and what the filters offer. */
export interface TrendsView {
  readonly locale: Locale;
  readonly page: TrendsPage;
  /** The modes a build targets, in the order of `/api/meta`. */
  readonly modes: readonly GameMode[];
  /** The request the page was read with, read again for the reader's own votes. */
  readonly request: ListTrends$Params;
}
