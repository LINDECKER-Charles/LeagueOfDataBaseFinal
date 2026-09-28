import type { EditorCatalogs } from '../catalog/editor-catalogs';
import type { StepEditing } from '../steps/step-editing';

/**
 * What the armory is opened with. The dialog lives at the root of the page, out of the
 * editor's injector: the editor hands over its purchase order and its lists, and the armory
 * provides them again.
 */
export interface ArmoryData {
  /** The index of the step the armory fills. */
  readonly step: number;
  readonly editing: StepEditing;
  readonly catalogs: EditorCatalogs;
}
