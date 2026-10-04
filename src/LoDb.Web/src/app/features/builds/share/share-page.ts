import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  linkedSignal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import type { SharedBuild } from '../../../core/api/generated/models/shared-build';
import { BuildsService } from '../../../core/api/generated/services/builds.service';
import { languageName } from '../../../core/api/meta/language-name';
import { injectRouteData } from '../../../core/routing/inject-route-data';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Chip } from '../../../ui/controls/chip';
import { CopyLink } from '../../../ui/controls/copy-link';
import { Image } from '../../../ui/media/image';
import { Backdrop } from '../../../ui/surfaces/backdrop';
import { shortDate } from '../shared/format/short-date';
import { imageSource } from '../shared/media/image-source';
import { initialsOf } from '../shared/media/initials-of';
import { modeLabelKey } from '../shared/modes/mode-label-key';
import { OwnerCredit } from '../shared/owner/owner-credit';
import { whenSignedIn } from '../shared/session/when-signed-in';
import { VoteScore } from '../shared/votes/vote-score';
import { pathClassOf } from './format/path-class-of';
import { applyShareHead } from './head/apply-share-head';
import type { SharedBuildPage } from './loading/shared-build-page';
import { ShareOrder } from './sections/share-order';
import { ShareRunes } from './sections/share-runes';

/**
 * A shared build, `/b/{token}`: the "strategy scroll" anyone holding its link reads, public
 * or private, rendered on the server on the patch it is pinned to and in its own language
 * (`resolveSharedBuild`). The champion's seal and the author, the patch it was forged on, its
 * runes and its purchase order, ghosts kept in place; the link to copy; the score and its
 * arrows on a public build only, the reader's own vote read again once signed in.
 */
@Component({
  selector: 'lodb-share-page',
  imports: [
    Backdrop,
    Chip,
    CopyLink,
    Image,
    OwnerCredit,
    ShareOrder,
    ShareRunes,
    TranslocoPipe,
    VoteScore,
  ],
  templateUrl: './share-page.html',
  styleUrl: './share-page.css',
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SharePage {
  protected readonly shared = injectRouteData<SharedBuildPage>('shared');
  protected readonly build = computed(() => this.shared().build);

  private readonly builds = inject(BuildsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly origin = inject(CANONICAL_ORIGIN);

  protected readonly vote = linkedSignal(() => this.build().vote ?? null);
  protected readonly pathClass = computed(() => pathClassOf(this.build().runes.primary.key));
  protected readonly portrait = computed(() => imageSource(this.build().champion.image));
  protected readonly initials = computed(() => initialsOf(this.build().champion.name));
  protected readonly modeKey = computed(() => modeLabelKey(this.build().gameMode));
  protected readonly updated = computed(() => shortDate(this.build().updatedAt));
  protected readonly language = computed(() => languageName(this.build().language));
  protected readonly languageTag = computed(() => this.build().language.replace('_', '-'));
  protected readonly link = computed(() => `${this.origin}/b/${this.build().shareToken}`);

  constructor() {
    applyShareHead(this.shared);
    whenSignedIn(this.build, (build) => this.readVote(build));
  }

  // The server rendered the score for nobody: the reader's own vote comes with a new read.
  private readVote(build: SharedBuild): void {
    if (!build.isPublic) {
      return;
    }
    this.builds
      .getSharedBuild({ token: build.shareToken })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (fresh) => {
          if (fresh.vote && fresh.shareToken === this.build().shareToken) {
            this.vote.set(fresh.vote);
          }
        },
        // The score read by the server stays; the arrows still vote.
        error: () => undefined,
      });
  }
}
