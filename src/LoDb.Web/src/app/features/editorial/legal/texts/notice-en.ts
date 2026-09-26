import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalText } from './legal-text';

/** The legal notice, in English. */
@Component({
  selector: 'lodb-notice-en',
  imports: [RouterLink],
  templateUrl: './notice-en.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NoticeEn extends LegalText {}
