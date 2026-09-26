import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { MyBuildRow } from '../../../../core/api/generated/models/my-build-row';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { Image } from '../../../../ui/media/image';
import { versionChoices } from '../../editor/context/version-choices';
import { imageSource } from '../../editor/shared/image-source';
import { initialsOf } from '../../editor/shared/initials-of';
import { updatedOn } from './updated-on';

/**
 * A build of the account's list: its champion with the keystone pinned on it, its name
 * linking to its share page, when it last changed and whether it is public; then the import
 * to another patch, the edit and the delete.
 */
@Component({
  selector: 'lodb-build-row',
  imports: [Button, Field, Image, RouterLink, TranslocoPipe],
  templateUrl: './build-row.html',
  styleUrl: './build-row.css',
  host: { class: 'hextech-frame hextech-frame-hover' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildRow {
  readonly row = input.required<MyBuildRow>();
  /** The patches Data Dragon lists, which an import may target. */
  readonly versions = input.required<readonly string[]>();
  /** The patch picked to import the build to. */
  readonly importTo = output<string>();
  /** The delete button pressed: the list asks before deleting. */
  readonly discard = output();

  private readonly locale = inject(PageDirection).locale;
  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
  protected readonly choices = computed(() =>
    versionChoices(this.versions(), this.row().gameVersion),
  );
  protected readonly shareLink = computed(() => `/b/${this.row().shareToken}`);
  protected readonly editLink = computed(() =>
    localePath(this.locale(), `account/builds/${this.row().id}/edit`),
  );
  protected readonly updated = computed(() => updatedOn(this.row().updatedAt, this.locale()));
  protected readonly selectId = computed(() => `lodb-import-to-${this.row().id}`);
}
