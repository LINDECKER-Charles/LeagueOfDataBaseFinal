import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Page envelope with the projection slots of the plan (section 5.2): `[lodbSlot=switcher]`,
 * `[lodbSlot=account]`, `[lodbSlot=banner]`, `[lodbSlot=contact]`, and the page itself as
 * default content. The slots are a contract: L3.1 plugs provisional components into them and
 * their chantiers replace those components, while L3.2 restyles this envelope. Neither
 * renames a slot.
 */
@Component({
  selector: 'lodb-shell',
  templateUrl: './shell.html',
  styleUrl: './shell.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {}
