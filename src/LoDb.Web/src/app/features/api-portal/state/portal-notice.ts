/** A banner of the portal: a return from Stripe, or its page that would not open. */
export interface PortalNotice {
  readonly tone: 'success' | 'error' | 'muted';
  /** Translation key of the message. */
  readonly key: string;
}
