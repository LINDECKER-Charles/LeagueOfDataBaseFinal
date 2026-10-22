import type { CanonicalLink } from '../../../../core/api/generated/models/canonical-link';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { PatchChangeKind } from '../../../../core/api/generated/models/patch-change-kind';
import type { ResourceType } from '../../../../core/api/generated/models/resource-type';

/** The entities of one resource a patch changed, as its page groups them. */
export interface PatchSection {
  readonly type: ResourceType;
  /** The resource's name, as the header's navigation gives it. */
  readonly title: string;
  readonly count: number;
  /** Changed, new, back, then removed: only the kinds the patch has. */
  readonly groups: readonly {
    readonly kind: PatchChangeKind;
    readonly entries: readonly {
      readonly id: string;
      readonly name: string;
      readonly image: CatalogImage;
      readonly link: CanonicalLink;
      /** The labels of the fields it changed, as one phrase; '' when none. */
      readonly fields: string;
    }[];
  }[];
}
