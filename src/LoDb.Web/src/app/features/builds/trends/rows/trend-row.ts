import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { TrendRow as TrendRowView } from '../../../../core/api/generated/models/trend-row';
import type { VoteState } from '../../../../core/api/generated/models/vote-state';
import { Chip } from '../../../../ui/controls/chip';
import { Image } from '../../../../ui/media/image';
import { BuildItem } from '../../shared/items/build-item';
import { languageLabel } from '../../shared/language/language-label';
import { imageSource } from '../../shared/media/image-source';
import { initialsOf } from '../../shared/media/initials-of';
import { modeLabelKey } from '../../shared/modes/mode-label-key';
import { OwnerCredit } from '../../shared/owner/owner-credit';
import { VoteScore } from '../../shared/votes/vote-score';

/**
 * A build of the trends: its score and arrows, its champion's portrait with the keystone
 * pinned on it and its name, both a link to its shared page; its author, mode, patch and
 * language; the first items of its purchase order, ghosts dimmed.
 */
@Component({
  selector: 'lodb-trend-row',
  imports: [BuildItem, Chip, Image, OwnerCredit, RouterLink, TranslocoPipe, VoteScore],
  templateUrl: './trend-row.html',
  styleUrl: './trend-row.css',
  host: { class: 'hextech-frame hextech-frame-hover trend-row' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrendRow {
  readonly row = input.required<TrendRowView>();
  /** The score to show: the row's own, or the one read again for the signed-in reader. */
  readonly vote = input.required<VoteState>();

  protected readonly portrait = computed(() => imageSource(this.row().champion.image));
  protected readonly initials = computed(() => initialsOf(this.row().champion.name));
  protected readonly keystone = computed(() => imageSource(this.row().keystone.icon));
  protected readonly modeKey = computed(() => modeLabelKey(this.row().gameMode));
  protected readonly language = computed(() => languageLabel(this.row().language));
}
