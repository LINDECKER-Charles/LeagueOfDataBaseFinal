import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Button } from '../../controls/button';
import { Chip } from '../../controls/chip';
import { Field } from '../../controls/field';
import { Icon } from '../../media/icon';

/** Buttons, chips and form fields, each in every tone and state. */
@Component({
  selector: 'lodb-gallery-controls',
  imports: [Button, Chip, Field, Icon],
  templateUrl: './controls-section.html',
  host: { class: 'block scroll-mt-20' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ControlsSection {}
