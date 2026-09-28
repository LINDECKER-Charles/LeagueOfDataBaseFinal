import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { LegalText } from './legal-text';

/** The terms of use, in English. */
@Component({
  selector: 'lodb-terms-en',
  imports: [FragmentLink, RouterLink],
  templateUrl: './terms-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsEn extends LegalText {}
