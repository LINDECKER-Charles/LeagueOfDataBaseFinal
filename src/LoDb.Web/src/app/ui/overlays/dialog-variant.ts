/**
 * A dialog that is a picture rather than a form, from the legacy viewers, which were not
 * native dialogs: `lightbox` is a frameless picture, 72rem at most (the skin viewer), and
 * `compact` a 30rem card the dialog draws itself (the chroma viewer). Both lie over a darker,
 * more blurred backdrop than a pane, and neither slides in: only their picture pops.
 */
export type DialogVariant = 'lightbox' | 'compact';
