import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The privacy policy, in French. */
@Component({
  selector: 'lodb-privacy-fr',
  imports: [RouterLink],
  templateUrl: './privacy-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyFr extends LegalText {}
