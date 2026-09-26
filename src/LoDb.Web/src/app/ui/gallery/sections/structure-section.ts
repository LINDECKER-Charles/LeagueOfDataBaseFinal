import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Disclosure } from '../../../core/layout/disclosure/disclosure';
import { Accordion } from '../../accordion/accordion';
import { AccordionItem } from '../../accordion/accordion-item';
import { Icon } from '../../media/icon';
import { Reveal } from '../../motion/reveal';
import { Pager } from '../../navigation/pager';
import { Tab } from '../../tabs/tab';
import { Tabs } from '../../tabs/tabs';

/** Accordion, tabs, header menus, pager, version chips and the scroll reveal. */
@Component({
  selector: 'lodb-gallery-structure',
  imports: [Accordion, AccordionItem, Disclosure, Icon, Pager, Reveal, Tab, Tabs],
  templateUrl: './structure-section.html',
  host: { class: 'block scroll-mt-20' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StructureSection {}
