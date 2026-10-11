import { type Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';
import type { ResourceType } from '../../../core/api/generated/models/resource-type';

/** The header's names of the four lists, translated in every locale. */
const LIST_LABEL_KEYS: Readonly<Record<ResourceType, string>> = {
  champions: 'header.navigation.champion',
  items: 'header.navigation.item',
  runes: 'header.navigation.runes',
  summoners: 'header.navigation.summoner',
};

type ListLabels = Readonly<Record<ResourceType, string>>;

/**
 * The names of the four lists in the active locale, translated once per catalogue rather
 * than by a pipe on each of the history's links (four per version, hundreds of versions):
 * every pipe would hold its own subscription.
 */
export function injectListLabels(): Signal<ListLabels> {
  const transloco = inject(TranslocoService);
  const translation = toSignal(transloco.selectTranslation(), { initialValue: {} });
  return computed(() => {
    translation();
    return {
      champions: transloco.translate(LIST_LABEL_KEYS.champions),
      items: transloco.translate(LIST_LABEL_KEYS.items),
      runes: transloco.translate(LIST_LABEL_KEYS.runes),
      summoners: transloco.translate(LIST_LABEL_KEYS.summoners),
    };
  });
}
