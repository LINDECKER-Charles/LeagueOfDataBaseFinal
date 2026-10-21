import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Field } from '../../../../../../../ui/controls/field';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { monthlyCost } from '../../../backups/monthly-cost';
import type { EpochChoice } from './epoch-choices';

/** A card of the choice, its words ready. */
interface EpochCard {
  readonly choice: EpochChoice;
  readonly title: string;
  readonly body: string;
  /** New: what it costs once complete, on top of the served one; '' otherwise. */
  readonly cost: string;
}

const EPOCH = 'prerender.launcher.epoch';

/**
 * « Epoch écrite » (§ 3.4): a card per epoch a pass may write, each a native radio in its
 * label, saying what writing it means; then « Ignorer ce qui est déjà complet », on by
 * default, aware of the types chosen, and hidden for a new epoch, which holds nothing.
 */
@Component({
  selector: 'lodb-epoch-cards',
  imports: [AdminTextPipe, Field],
  template: `
    <section class="mt-6">
      <h3 id="launch-epoch-title" class="readout-label mb-2">
        {{ 'admin.prerender.launcher.epoch.title' | adminText }}
      </h3>
      <div class="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-labelledby="launch-epoch-title">
        @for (card of cards(); track card.choice.epoch) {
          <label
            class="flex cursor-pointer flex-col gap-1.5 border border-gold/28 p-3 has-checked:border-hex has-checked:bg-hex/5"
            [attr.data-epoch]="card.choice.epoch"
          >
            <span class="flex items-center gap-2 text-gold-bright">
              <input
                type="radio"
                name="launch-epoch"
                class="size-4 shrink-0"
                [checked]="card.choice.epoch === epoch()"
                (change)="chosen.emit(card.choice)"
              />
              {{ card.title }}
            </span>
            <span class="readout-text">{{ card.body }}</span>
            @if (card.cost) {
              <span class="readout-text">{{ card.cost }}</span>
            }
          </label>
        }
      </div>
      @if (kind() !== 'new') {
        <label class="mt-4 flex items-start gap-3" data-skip>
          <input
            type="checkbox"
            lodbField="switch"
            [checked]="skipComplete()"
            (change)="skip($event)"
          />
          <span>
            <span class="text-gold-bright">
              {{ 'admin.prerender.launcher.skip.label' | adminText: { epoch: epoch() } }}
            </span>
            @if (skipComplete() && skipped() !== null) {
              <span class="text-text-muted" data-skip-count>
                — {{ 'admin.prerender.launcher.skip.count' | adminText: { units: skipped() } }}
              </span>
            }
            <span class="readout-text block">
              {{ 'admin.prerender.launcher.skip.hint' | adminText }}
            </span>
          </span>
        </label>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EpochCards {
  readonly choices = input.required<readonly EpochChoice[]>();
  /** The epoch chosen. */
  readonly epoch = input.required<number>();
  readonly skipComplete = input.required<boolean>();
  /** The units « Ignorer… » leaves out; null while the coverage loads. */
  readonly skipped = input.required<number | null>();
  readonly chosen = output<EpochChoice>();
  readonly skipChanged = output<boolean>();

  private readonly texts = injectAdminText();

  protected readonly kind = computed(
    () => this.choices().find((choice) => choice.epoch === this.epoch())?.kind ?? 'new',
  );
  protected readonly cards = computed<EpochCard[]>(() => {
    const served = this.choices()[0]?.epoch ?? 0;
    return this.choices().map((choice) => this.cardOf(choice, served));
  });

  protected skip(event: Event): void {
    this.skipChanged.emit((event.target as HTMLInputElement).checked);
  }

  private cardOf(choice: EpochChoice, served: number): EpochCard {
    const { kind, epoch } = choice;
    const share = choice.share === null ? '—' : figure(choice.share, 'share');
    const cost = monthlyCost(choice.monthlyCost, choice.currency);
    return {
      choice,
      title: this.texts.text(`${EPOCH}.${kind}_title`, { epoch }),
      body: this.texts.text(`${EPOCH}.${kind}_body`, { epoch, share }),
      cost: kind === 'new' ? this.texts.text(`${EPOCH}.new_cost`, { cost, served }) : '',
    };
  }
}
