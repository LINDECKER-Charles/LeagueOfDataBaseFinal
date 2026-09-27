import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FragmentLink } from '../../../../ui/navigation/fragment-link';
import { LegalText } from './legal-text';

/** The terms of use, in French. */
@Component({
  selector: 'lodb-terms-fr',
  imports: [FragmentLink, RouterLink],
  templateUrl: './terms-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TermsFr extends LegalText {}
