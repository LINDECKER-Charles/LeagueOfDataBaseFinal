import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The privacy policy, in English. */
@Component({
  selector: 'lodb-privacy-en',
  imports: [RouterLink],
  templateUrl: './privacy-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyEn extends LegalText {}
