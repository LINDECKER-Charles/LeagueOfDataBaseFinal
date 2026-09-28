import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Image } from '../../../../ui/media/image';
import { EditorCatalogs } from '../catalog/editor-catalogs';
import { CatalogState } from '../catalog/catalog-state';
import { BuildEditorStore } from '../form/build-editor-store';
import { imageSource } from '../shared/image-source';
import { initialsOf } from '../shared/initials-of';
import { SearchIndex } from '../shared/search-index';

/**
 * The champion of the build: a header naming the one picked, which folds a searchable grid
 * of portraits away. The grid starts open on a build without a champion and folds once one
 * is picked. A champion the patch lacks stays picked, flagged as a ghost.
 */
@Component({
  selector: 'lodb-champion-picker',
  imports: [CatalogState, Image, TranslocoPipe],
  templateUrl: './champion-picker.html',
  styleUrl: './champion-picker.css',
  host: { class: 'block space-y-4' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionPicker {
  private readonly store = inject(BuildEditorStore);
  protected readonly champions = inject(EditorCatalogs).champions;
  protected readonly championId = this.store.championId;
  protected readonly imageSource = imageSource;
  protected readonly initialsOf = initialsOf;
  protected readonly query = signal('');
  protected readonly isOpen = signal(this.championId() === '');

  private readonly index = computed(
    () =>
      new SearchIndex(this.champions.options() ?? [], (option) => `${option.name} ${option.id}`),
  );
  protected readonly shown = computed(() => this.index().matching(this.query()));
  protected readonly selected = computed(
    () => this.champions.options()?.find((option) => option.id === this.championId()) ?? null,
  );
  protected readonly isGhost = computed(
    () => this.championId() !== '' && this.champions.options() !== null && this.selected() === null,
  );

  protected toggle(): void {
    this.isOpen.update((open) => !open);
  }

  /** Picks a champion, and folds the grid: the choice is made. */
  protected choose(id: string): void {
    this.store.championId.set(id);
    this.isOpen.set(false);
  }
}
