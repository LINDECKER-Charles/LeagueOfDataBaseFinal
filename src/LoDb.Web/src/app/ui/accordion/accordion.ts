import { CdkAccordion } from '@angular/cdk/accordion';
import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Group of accordion items; with `multi` several items stay open at once, otherwise opening
 * one closes the others. The CDK accordion brings the coordination.
 */
@Component({
  selector: 'lodb-accordion',
  template: '<ng-content />',
  hostDirectives: [{ directive: CdkAccordion, inputs: ['multi'] }],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Accordion {}
