/** A banner of the portal: the outcome of the last action, or of a return from Stripe. */
export interface PortalNotice {
  readonly tone: 'success' | 'error' | 'muted';
  /** Translation key of the message. */
  readonly key: string;
}
