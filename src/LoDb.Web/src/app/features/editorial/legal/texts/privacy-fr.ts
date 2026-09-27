import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { LegalText } from './legal-text';

/** The privacy policy, in French. */
@Component({
  selector: 'lodb-privacy-fr',
  imports: [FragmentLink, RouterLink],
  templateUrl: './privacy-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrivacyFr extends LegalText {}
