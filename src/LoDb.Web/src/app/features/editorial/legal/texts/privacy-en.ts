import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { LegalText } from './legal-text';

/** The privacy policy, in English. */
@Component({
  selector: 'lodb-privacy-en',
  imports: [FragmentLink, RouterLink],
  templateUrl: './privacy-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyEn extends LegalText {}
