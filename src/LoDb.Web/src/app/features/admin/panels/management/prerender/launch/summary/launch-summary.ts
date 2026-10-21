import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { Kpi } from '../../../../../widgets/kpi';
import { monthlyCost } from '../../../backups/monthly-cost';
import { unitPrice } from '../../../backups/unit-price';
import { scopeSummary } from '../../common/scope/scope-summary';
import { decimalBytes } from '../../format/decimal-bytes';
import type { LaunchErrors, LaunchMessage } from '../call/launch-errors-of';
import { requestOfDraft } from '../call/request-of-draft';
import type { LaunchDraft } from '../dialog/launch-draft';
import type { LaunchEstimate } from './launch-estimate';
import type { LaunchWarning } from './launch-warnings';

/** A tile of the summary, its words ready. */
interface SummaryTile {
  readonly key: Tile;
  readonly label: string;
  readonly value: string;
  readonly sub: string;
}

const SUMMARY = 'prerender.launcher.summary';
// The tiles, in their order; each one a key of the summary.
const TILES = ['units', 'pages', 'volume', 'duration', 'cost'] as const;
type Tile = (typeof TILES)[number];
const LIST_SEPARATOR = ', ';
// The figure quality_default names: pagesPerLocaleOf's stand-in without any measure.
const DEFAULT_PAGES = 500;

/**
 * The summary of a launch (§ 3.5), which is its confirmation: the epoch and the perimeter,
 * five tiles (units, pages, volume, duration, added cost), how good the estimate is and how
 * it is made, the warnings, the order of the pass and the pass it was prefilled from; then
 * every error the API answered.
 */
@Component({
  selector: 'lodb-launch-summary',
  imports: [AdminTextPipe, Kpi],
  template: `
    <p class="text-gold-bright" data-summary-scope>{{ scope() }}</p>
    <div class="kpi-grid summary-tiles mt-4">
      @for (tile of tiles(); track tile.key) {
        <!-- The figure set smaller than a page's: five tiles share the dialog's width. -->
        <lodb-kpi [label]="tile.label" [value]="null" [sub]="tile.sub" [attr.data-tile]="tile.key">
          <span class="summary-value" data-tile-value>{{ tile.value }}</span>
        </lodb-kpi>
      }
    </div>
    <p class="readout-text mt-3" data-quality>{{ quality() }}</p>
    <details class="mt-2" data-how>
      <summary class="cursor-pointer text-[0.88rem] text-gold-bright">
        {{ 'admin.prerender.launcher.summary.how' | adminText }}
      </summary>
      <ul class="mt-2 grid gap-1 font-mono text-[0.8rem] text-text-muted">
        @for (line of how(); track $index) {
          <li>{{ line }}</li>
        }
      </ul>
    </details>
    @if (warnings().length > 0) {
      <ul class="mt-4 grid gap-2 text-[0.88rem]" data-warnings>
        @for (warning of warnings(); track warning.code) {
          <li
            [class]="warning.tone === 'bad' ? 'text-danger-light' : 'text-gold'"
            [attr.data-warning]="warning.code"
          >
            ⚠ {{ 'admin.prerender.launcher.warnings.' + warning.code | adminText: warning.params }}
          </li>
        }
      </ul>
    }
    <p class="readout-text mt-4" data-order>{{ order() }}</p>
    @if (draft().fromPass; as id) {
      <p class="readout-text mt-1" data-from-pass>
        {{ 'admin.prerender.launcher.from_pass' | adminText: { id } }}
      </p>
    }
    @if (messages().length > 0) {
      <ul class="mt-4 grid gap-1 text-[0.88rem] text-danger-light" role="alert" data-errors>
        @for (message of messages(); track $index) {
          <li>{{ 'admin.' + message.key | adminText: message.params }}</li>
        }
      </ul>
    }
  `,
  // Five tiles in a row of the dialog: the page's 11rem leaves the fifth alone, its figure cut.
  styles: `
    .kpi-grid.summary-tiles {
      grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    }
    .summary-value {
      display: block;
      margin-block-start: 0.375rem;
      font-family: var(--font-beaufort);
      font-size: 1.35rem;
      line-height: 1.1;
      color: var(--color-gold-bright);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LaunchSummary {
  readonly draft = input.required<LaunchDraft>();
  readonly estimate = input.required<LaunchEstimate>();
  /** The coverage of the epoch written: the price and the versions, newest first. */
  readonly coverage = input.required<PrerenderPatchesReport>();
  readonly warnings = input.required<readonly LaunchWarning[]>();
  readonly errors = input<LaunchErrors | null>(null);

  private readonly texts = injectAdminText();

  protected readonly scope = computed(() => {
    const draft = this.draft();
    const request = requestOfDraft(draft, this.coverage());
    const versions = request.versions ?? null;
    const perimeter = scopeSummary(
      {
        versionCount: versions?.length ?? null,
        newestListed: versions?.[0] ?? null,
        oldestListed: versions?.at(-1) ?? null,
        locales: request.locales,
        pageTypes: request.pageTypes,
        priority: request.priority ?? [],
        skipComplete: request.skipComplete ?? false,
      },
      this.texts,
    );
    return this.text('scope', { epoch: draft.epoch, kind: draft.epochKind, perimeter });
  });
  protected readonly tiles = computed(() => this.tilesOf(this.estimate()));
  protected readonly quality = computed(() => {
    const { isDefault, guesses, measured, versions } = this.estimate();
    if (isDefault) {
      return this.text('quality_default', { pages: DEFAULT_PAGES });
    }
    return guesses.length === 0
      ? this.text('quality_measured')
      : this.text('quality_mixed', { known: measured, total: versions, guessed: guesses.length });
  });
  protected readonly how = computed(() => this.howOf(this.estimate()));
  protected readonly order = computed(() => {
    const { priority } = this.draft().selection;
    return priority.length === 0
      ? this.text('order_default')
      : this.text('order', { first: priority.join(LIST_SEPARATOR) });
  });
  protected readonly messages = computed<LaunchMessage[]>(() =>
    Object.values(this.errors()?.fields ?? {}).flat(),
  );

  private text(key: string, params?: Readonly<Record<string, unknown>>): string {
    return this.texts.text(`${SUMMARY}.${key}`, params);
  }

  private tilesOf(estimate: LaunchEstimate): SummaryTile[] {
    const values = this.valuesOf(estimate);
    const subs = this.subsOf(estimate);
    return TILES.map((key) => ({ key, label: this.text(key), value: values[key], sub: subs[key] }));
  }

  private valuesOf(estimate: LaunchEstimate): Readonly<Record<Tile, string>> {
    const { currency } = this.coverage().estimates;
    return {
      units: figure(estimate.units),
      pages: `≈ ${figure(estimate.pages)}`,
      volume: `≈ ${decimalBytes(estimate.bytes)}`,
      duration: `≈ ${duration(estimate.seconds)}`,
      cost: this.text('cost_value', { cost: monthlyCost(estimate.addedCost, currency) }),
    };
  }

  private subsOf(estimate: LaunchEstimate): Readonly<Record<Tile, string>> {
    const { currency, pricePerGbMonthOneZone: price } = this.coverage().estimates;
    const perUnit = estimate.units > 0 ? estimate.pages / estimate.units : 0;
    return {
      units: this.text('units_sub', { ...estimate }),
      pages: this.text('pages_sub', { perUnit: figure(perUnit) }),
      volume: this.text('volume_sub', { perPage: decimalBytes(estimate.bytesPerPage) }),
      duration: this.text(`duration_${estimate.paceSource}`, { pace: Math.round(estimate.pace) }),
      cost: this.text('cost_sub', { price: unitPrice(price, currency) }),
    };
  }

  private howOf(estimate: LaunchEstimate): string[] {
    const { currency, pricePerGbMonthOneZone: price } = this.coverage().estimates;
    const { units, pages, share, bytesPerPage, seconds } = estimate;
    const perUnit = units > 0 && share > 0 ? Math.round(pages / share / units) : 0;
    const pace = Math.round(estimate.pace);
    const size = decimalBytes(estimate.bytes);
    return [
      this.text('how_units', { ...estimate }),
      this.text('how_pages', { pages, units, perUnit, share: figure(share, 'percent') }),
      this.text('how_volume', { size, pages, perPage: decimalBytes(bytesPerPage) }),
      this.text('how_duration', { duration: duration(seconds), pages, pace }),
      this.text('how_cost', {
        cost: monthlyCost(estimate.addedCost, currency),
        size: decimalBytes(estimate.addedBytes),
        price: unitPrice(price, currency),
      }),
      ...this.guessesOf(estimate),
    ];
  }

  // The patches not measured and what stood in for each; none without any measure at all.
  private guessesOf(estimate: LaunchEstimate): string[] {
    if (estimate.isDefault) {
      return [];
    }
    return estimate.guesses.map((guess) =>
      this.text('how_guess', {
        version: guess.version,
        source: guess.source === 'season' ? 'season' : 'other',
        season: `${guess.from}.x`,
        neighbour: guess.from,
        pages: guess.pages,
      }),
    );
  }
}
