import { Directive, inject } from '@angular/core';
import { AudienceOptOut } from '../../../../core/analytics/audience/send/audience-opt-out';
import { LegalText } from './legal-text';

/**
 * A privacy policy, in either language: a legal text that also holds the visitor's choice
 * about the audience measure, a switch kept on this browser only.
 */
@Directive()
export abstract class PrivacyText extends LegalText {
  protected readonly audience = inject(AudienceOptOut);

  protected measure(event: Event): void {
    this.audience.choose((event.target as HTMLInputElement).checked);
  }
}
