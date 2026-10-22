import type { PatchChangeGroup } from '../../../../core/api/generated/models/patch-change-group';
import type { PatchChangeKind } from '../../../../core/api/generated/models/patch-change-kind';
import type { ResourceType } from '../../../../core/api/generated/models/resource-type';
import type { ValueFormat } from '../../shared/codex/state/changes/value-format';
import { fieldLabelsOf } from '../../shared/codex/state/labels/field-labels-of';
import { listOf } from '../../shared/codex/state/labels/list-of';
import type { PatchSection } from './patch-section';

/** The order of a resource's groups: what most readers look for first. */
const KIND_ORDER: readonly PatchChangeKind[] = ['changed', 'new', 'returned', 'retired'];

/** The header's names of the four lists, translated in every locale. */
const RESOURCE_LABEL_KEYS: Readonly<Record<ResourceType, string>> = {
  champions: 'header.navigation.champion',
  items: 'header.navigation.item',
  runes: 'header.navigation.runes',
  summoners: 'header.navigation.summoner',
};

function sectionOf(group: PatchChangeGroup, format: ValueFormat): PatchSection {
  const entries = group.entries.map((entry) => ({
    id: entry.id,
    name: entry.name,
    image: entry.image,
    link: entry.link,
    kind: entry.kind,
    fields: listOf(format.locale, fieldLabelsOf(entry.changedFields, format.translate)),
  }));
  return {
    type: group.type,
    title: format.translate(RESOURCE_LABEL_KEYS[group.type]),
    count: entries.length,
    groups: KIND_ORDER.map((kind) => ({
      kind,
      entries: entries.filter((entry) => entry.kind === kind),
    })).filter((kind) => kind.entries.length > 0),
  };
}

/**
 * The sections of the page of a patch's changes: one per resource the API lists, in its
 * order, each grouping its entities by what the patch did to them, the fields a change or a
 * return touched named by their labels.
 */
export function patchSectionsOf(
  groups: readonly PatchChangeGroup[],
  format: ValueFormat,
): PatchSection[] {
  return groups.map((group) => sectionOf(group, format));
}
