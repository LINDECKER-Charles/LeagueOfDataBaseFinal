import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The cookie policy, in French. */
@Component({
  selector: 'lodb-cookies-fr',
  imports: [RouterLink],
  templateUrl: './cookies-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CookiesFr extends LegalText {}
