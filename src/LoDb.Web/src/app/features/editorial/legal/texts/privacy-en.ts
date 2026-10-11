import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Field } from '../../../../ui/controls/field';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { PrivacyText } from './privacy-text';

/** The privacy policy, in English. */
@Component({
  selector: 'lodb-privacy-en',
  imports: [Field, FragmentLink, RouterLink],
  templateUrl: './privacy-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyEn extends PrivacyText {}
