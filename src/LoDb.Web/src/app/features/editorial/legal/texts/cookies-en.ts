import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The cookie policy, in English. */
@Component({
  selector: 'lodb-cookies-en',
  imports: [RouterLink],
  templateUrl: './cookies-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CookiesEn extends LegalText {}
