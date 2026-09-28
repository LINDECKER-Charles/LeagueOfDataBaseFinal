/**
 * A save of one part of the profile, sent when the autosave runs it: it answers `saved`, or
 * `warned` when the server stored it with something dropped, and fails with the call.
 */
export type ProfileSave = () => Promise<'saved' | 'warned'>;
