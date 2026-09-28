/** What the switcher's form holds: a version and the key of a language option. */
export interface SwitcherChoice {
  readonly version: string;
  /** `{locale}:{language}`, the key of a {@link LanguageOption}. */
  readonly language: string;
}
