import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { Button } from '../../../../../../../ui/controls/button';
import { Field } from '../../../../../../../ui/controls/field';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { versionTokens } from '../../common/selection/version-tokens';
import { type PasteResult, pasteResultOf } from './paste-result-of';

/** A pasted list once applied: the older patches it names, and how they rank. */
export interface PastedVersions {
  readonly versions: readonly string[];
  /** Whether they go after the priorities already set, rather than replace them. */
  readonly append: boolean;
  /** Whether more than 100 were named: the ones past it are ticked without priority. */
  readonly isCapped: boolean;
}

const PASTE = 'prerender.launcher.paste';
const LIST_SEPARATOR = ', ';

/**
 * « Coller une liste de versions (journal de l'edge) » (§ 3.3.7): a text area that reads any
 * list (a version a line, commas, LogsQL's tabulated output, JSON), whether the versions
 * replace the priorities or follow them, and « Appliquer », which ticks each older patch it
 * names and ranks it. What it ignored is said under it; the text stays, to correct it.
 */
@Component({
  selector: 'lodb-paste-box',
  imports: [AdminTextPipe, Button, Field],
  template: `
    <details class="mt-4" data-paste>
      <summary class="cursor-pointer text-[0.88rem] text-gold-bright">
        {{ 'admin.prerender.launcher.paste.toggle' | adminText }}
      </summary>
      <div class="mt-3 grid gap-3">
        <label class="grid gap-1.5">
          <span class="readout-label">{{
            'admin.prerender.launcher.paste.label' | adminText
          }}</span>
          <textarea
            lodbField
            rows="4"
            class="w-full font-mono"
            [value]="text()"
            (input)="read($event)"
          ></textarea>
          <span class="readout-text">{{ 'admin.prerender.launcher.paste.hint' | adminText }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-4 text-[0.88rem]">
          <label class="inline-flex min-h-11 items-center gap-2">
            <input
              type="radio"
              name="paste-rank"
              class="accent-hex scheme-dark"
              [checked]="!append()"
              (change)="append.set(false)"
            />
            {{ 'admin.prerender.launcher.paste.replace' | adminText }}
          </label>
          <label class="inline-flex min-h-11 items-center gap-2">
            <input
              type="radio"
              name="paste-rank"
              class="accent-hex scheme-dark"
              [checked]="append()"
              (change)="append.set(true)"
            />
            {{ 'admin.prerender.launcher.paste.append' | adminText }}
          </label>
          <button lodbButton="ghost" lodbButtonSize="small" type="button" (click)="apply()">
            {{ 'admin.prerender.launcher.paste.apply' | adminText }}
          </button>
        </div>
        @if (outcome(); as said) {
          <p class="readout-text" role="status" data-paste-result>{{ said }}</p>
        }
      </div>
    </details>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasteBox {
  /** The coverage of the epoch: the older patches, and the latest one. */
  readonly patches = input.required<PrerenderPatchesReport>();
  readonly pasted = output<PastedVersions>();

  protected readonly text = signal('');
  protected readonly append = signal(false);
  private readonly result = signal<PasteResult | null>(null);
  private readonly texts = injectAdminText();

  protected readonly outcome = computed(() => {
    const result = this.result();
    return result ? this.said(result) : '';
  });

  protected read(event: Event): void {
    this.text.set((event.target as HTMLTextAreaElement).value);
  }

  protected apply(): void {
    const result = pasteResultOf(versionTokens(this.text()), this.patches());
    this.result.set(result);
    this.pasted.emit({ versions: result.known, append: this.append(), isCapped: result.isCapped });
  }

  private said(result: PasteResult): string {
    const ignored = [
      ...result.latest.map((version) => this.texts.text(`${PASTE}.latest`, { version })),
      ...result.unknown.map((version) => this.texts.text(`${PASTE}.unknown`, { version })),
    ];
    return [
      this.texts.text(`${PASTE}.known`, { count: result.known.length }),
      ignored.length > 0
        ? this.texts.text(`${PASTE}.ignored`, { list: ignored.join(LIST_SEPARATOR) })
        : '',
      result.isCapped ? this.texts.text(`${PASTE}.capped`) : '',
    ]
      .filter((part) => part !== '')
      .join(' · ');
  }
}
