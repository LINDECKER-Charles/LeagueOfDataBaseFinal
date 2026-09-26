import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The terms of use, in English. */
@Component({
  selector: 'lodb-terms-en',
  imports: [RouterLink],
  templateUrl: './terms-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsEn extends LegalText {}
