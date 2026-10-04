import type { BuildRequest } from '../../../../core/api/generated/models/build-request';
import type { BuildEditorStore } from '../form/build-editor-store';
import type { RuneDraft } from '../runes/rune-draft';
import type { PurchaseOrder } from '../steps/order/purchase-order';

/** What the editor holds when its author saves: the fields, the runes, the purchase order. */
interface EditorState {
  readonly store: BuildEditorStore;
  readonly runes: RuneDraft;
  readonly order: PurchaseOrder;
}

/**
 * The build as the API takes it, as typed: the API trims, checks and names what it
 * refuses. A blank description is sent as none.
 */
export function buildRequestOf({ store, runes, order }: EditorState): BuildRequest {
  const description = store.description();
  return {
    name: store.name(),
    description: description.trim() === '' ? null : description,
    isPublic: store.isPublic(),
    gameVersion: store.gameVersion(),
    gameMode: store.gameMode(),
    language: store.language(),
    structure: {
      championId: store.championId(),
      runes: runes.toRunePage(),
      steps: order.toSteps(),
    },
  };
}
