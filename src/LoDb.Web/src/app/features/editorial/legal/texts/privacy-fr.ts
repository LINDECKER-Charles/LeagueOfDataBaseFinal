import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Field } from '../../../../ui/controls/field';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { PrivacyText } from './privacy-text';

/** The privacy policy, in French. */
@Component({
  selector: 'lodb-privacy-fr',
  imports: [Field, FragmentLink, RouterLink],
  templateUrl: './privacy-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyFr extends PrivacyText {}
