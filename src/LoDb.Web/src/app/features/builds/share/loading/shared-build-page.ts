import type { SharedBuild } from '../../../../core/api/generated/models/shared-build';
import type { Locale } from '../../../../core/i18n/locales';

/** What the route of `/b/{token}` resolves: the build, and the locale its page speaks. */
export interface SharedBuildPage {
  readonly build: SharedBuild;
  /** The build's own language as an interface locale, `en` by default. */
  readonly locale: Locale;
}
