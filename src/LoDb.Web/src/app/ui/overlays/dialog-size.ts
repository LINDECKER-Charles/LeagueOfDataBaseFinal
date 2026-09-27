/**
 * Width of a centred dialog, from the legacy panes it replaces: `form` is the 34rem contact
 * form, `picker` the 32rem favorite and skin pickers, `wide` the 56rem armory. Without one, a
 * dialog is 46rem wide, the theme picker's width. A sheet takes the same name as a modifier
 * of its `hx-sheet` pane, which a variant styles when it needs to.
 *
 * `lightbox` and `compact` are the legacy viewers, which were no native dialogs: a frameless
 * picture 72rem at most (the skin splash) and a 30rem card (the chroma). The dialog draws its
 * own surface on a pane of their own, over a darker, more blurred backdrop, with no slide-in.
 */
export type DialogSize = 'form' | 'picker' | 'wide' | 'lightbox' | 'compact';
