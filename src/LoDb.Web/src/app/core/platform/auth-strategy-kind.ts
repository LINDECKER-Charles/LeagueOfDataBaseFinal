/**
 * Authentication strategy of a platform (plan, section 5.2): session cookie on the web (L4.5),
 * tokens held by the desktop host (L9.3), bearer tokens with refresh on Android (L10.2).
 * `core/auth` picks its `AuthStrategy` implementation from this value.
 */
export type AuthStrategyKind = 'cookie' | 'host' | 'bearer';
