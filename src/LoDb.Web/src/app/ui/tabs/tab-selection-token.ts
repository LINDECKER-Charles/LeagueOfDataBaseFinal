import { InjectionToken } from '@angular/core';
import type { TabSelection } from './tab-selection';

/** The enclosing tab group, reached through a token so a tab never imports its group. */
export const TAB_SELECTION = new InjectionToken<TabSelection>('TAB_SELECTION');
