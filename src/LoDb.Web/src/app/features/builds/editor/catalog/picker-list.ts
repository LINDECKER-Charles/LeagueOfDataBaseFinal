import { type ResourceRef, type Signal, computed } from '@angular/core';
import { type CatalogStatus, catalogStatus } from './catalog-status';

/**
 * One picker list of the editor over its resource: its options, null until they are read
 * for the current patch (and mode), where it stands, and a way to try again.
 */
export class PickerList<T> {
  readonly options: Signal<T | null>;
  readonly status: Signal<CatalogStatus>;

  constructor(private readonly resource: ResourceRef<T | undefined>) {
    this.options = computed(() => (resource.hasValue() ? resource.value() : null));
    this.status = computed(() => catalogStatus(resource.status()));
  }

  retry(): void {
    this.resource.reload();
  }
}
