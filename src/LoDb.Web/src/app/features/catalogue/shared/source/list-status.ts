/**
 * Where a list page stands: its list is on its way (`loading`), shown (`ready`), not
 * ingested yet for this version (`pending`, placeholders shown), or out of reach (`failed`).
 */
export type ListStatus = 'loading' | 'ready' | 'pending' | 'failed';
