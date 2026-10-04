import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The legal notice, in French. */
@Component({
  selector: 'lodb-notice-fr',
  imports: [RouterLink],
  templateUrl: './notice-fr.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NoticeFr extends LegalText {}
