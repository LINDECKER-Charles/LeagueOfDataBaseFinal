import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  PLATFORM_ID,
  afterNextRender,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { filter, map } from 'rxjs';
import type { CatalogMeta } from '../../core/api/generated/models/catalog-meta';
import { ApiMeta } from '../../core/api/meta/api-meta';
import { PreferencesStore } from '../../core/context/preferences/preferences-store';
import { switchContext } from '../../core/context/switch/switch-context';
import { Disclosure } from '../../core/layout/disclosure/disclosure';
import { PageDirection } from '../../core/layout/direction/page-direction';
import { NavContext } from '../../core/layout/nav/nav-context';
import { Button } from '../../ui/controls/button';
import { Field } from '../../ui/controls/field';
import { Icon } from '../../ui/media/icon';
import { languageOptions } from './options/language-options';
import { versionOptions } from './options/version-options';
import { currentChoice } from './selection/current-choice';
import { preferencesOf } from './selection/preferences-of';
import { rememberedTarget } from './selection/remembered-target';
import { targetOf } from './selection/target-of';

/** The Transloco scope of the switcher's own texts (`public/i18n/context-switcher/`). */
const SCOPE = 'context-switcher';
const LOCALE_SUBTAG_SEPARATOR = '-';
// A patch number in the chip's monospace: seven letters and their 0.06em tracking.
const PATCH_WIDTH = '7.7ch';

function valueOf(event: Event): string {
  return (event.target as HTMLSelectElement).value;
}

/**
 * Patch and language switcher of the header (`switcher` slot): a native `<details>` holding
 * a form that never posts. Submitting navigates to the same page in the chosen context, the
 * URL rewritten by `switchContext`; "remember" writes the choice into `lod_prefs`, and the
 * switcher applies that cookie to the pages that name no context of their own.
 *
 * The header sits on prerendered pages too, so the options (versions, languages) load in the
 * browser, after the first render, never during one. The chip names the page's version from
 * the first paint all the same: a server render resolves it (`NavContext`) and hands it to
 * the browser. A prerendered page names none; its placeholder holds the version's width, so
 * the chip does not jump when the options arrive.
 */
@Component({
  selector: 'lodb-context-switcher',
  imports: [Button, Disclosure, Field, Icon, TranslocoPipe],
  providers: [provideTranslocoScope(SCOPE)],
  templateUrl: './context-switcher.html',
  host: { class: 'block shrink-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContextSwitcher {
  private readonly router = inject(Router);
  private readonly apiMeta = inject(ApiMeta);
  private readonly preferences = inject(PreferencesStore);
  private readonly page = inject(PageDirection);
  private readonly nav = inject(NavContext);
  private readonly destroyRef = inject(DestroyRef);
  private readonly panel = viewChild.required<ElementRef<HTMLDetailsElement>>('panel');
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** `/api/meta`, once the browser has loaded it; null on the server. */
  protected readonly meta = signal<CatalogMeta | null>(null);
  protected readonly unavailable = signal(false);
  protected readonly versions = computed(() => this.optionsOf(versionOptions));
  protected readonly languages = computed(() => this.optionsOf(languageOptions));
  protected readonly current = computed(() => {
    const meta = this.meta();
    const shown = { url: this.url(), locale: this.page.locale() };
    return meta === null ? null : currentChoice(shown, meta, this.languages());
  });
  protected readonly version = linkedSignal(() => this.current()?.version ?? '');
  protected readonly language = linkedSignal(() => this.current()?.language ?? '');
  protected readonly remember = signal(false);
  /** `EN` on `/en/`: every language the page may read shares it. */
  protected readonly languageCode = computed(() =>
    this.page.locale().split(LOCALE_SUBTAG_SEPARATOR, 1)[0].toUpperCase(),
  );
  protected readonly placeholderWidth = PATCH_WIDTH;
  /** The version the chip names: the page's, known before the options are. */
  protected readonly shownVersion = computed(
    () => this.current()?.version ?? this.nav.selection()?.shown ?? null,
  );
  /** The context the chip stands for, in its accessible name even where the patch is hidden. */
  protected readonly shown = computed(() => {
    const version = this.shownVersion();
    return version === null ? this.languageCode() : `${version}, ${this.languageCode()}`;
  });

  constructor() {
    if (isPlatformBrowser(inject(PLATFORM_ID))) {
      afterNextRender(() => this.load());
    }
    effect(() => {
      const meta = this.meta();
      const url = this.url();
      if (meta !== null) {
        untracked(() => this.followRemembered(url, meta));
      }
    });
  }

  protected pickVersion(event: Event): void {
    this.version.set(valueOf(event));
  }

  protected pickLanguage(event: Event): void {
    this.language.set(valueOf(event));
  }

  protected toggleRemember(event: Event): void {
    this.remember.set((event.target as HTMLInputElement).checked);
  }

  /** Goes to the same page in the chosen context: one navigation, no POST, no redirect. */
  protected apply(event: Event): void {
    event.preventDefault();
    const meta = this.meta();
    const language = this.languages().find((option) => option.key === this.language());
    if (meta === null || language === undefined) {
      return;
    }
    const target = targetOf(this.version(), language);
    this.preferences.remember(this.remember() ? preferencesOf(target, meta) : null);
    this.panel().nativeElement.open = false;
    void this.router.navigateByUrl(switchContext(this.router.url, target, meta));
  }

  private optionsOf<T>(build: (meta: CatalogMeta) => T[]): T[] {
    const meta = this.meta();
    return meta === null ? [] : build(meta);
  }

  private load(): void {
    this.remember.set(this.preferences.read() !== null);
    this.apiMeta
      .meta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (meta) => this.meta.set(meta),
        error: () => this.unavailable.set(true),
      });
  }

  // The remembered context fills what the URL leaves unsaid; the history entry is replaced,
  // so Back does not return to the page the cookie rewrote.
  private followRemembered(url: string, meta: CatalogMeta): void {
    const remembered = this.preferences.read();
    const target = remembered === null ? null : rememberedTarget(url, remembered, meta);
    if (target === null) {
      return;
    }
    const next = switchContext(url, target, meta);
    if (next !== url) {
      void this.router.navigateByUrl(next, { replaceUrl: true });
    }
  }
}
