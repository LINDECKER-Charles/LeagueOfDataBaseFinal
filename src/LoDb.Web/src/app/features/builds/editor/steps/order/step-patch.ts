import type { OrderStep } from './order-step';

/** What the author types into a step: its label, its note, or both. */
export type StepPatch = Partial<Pick<OrderStep, 'label' | 'note'>>;
