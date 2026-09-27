/**
 * Width of a centred dialog, from the legacy panes it replaces: `form` is the 34rem contact
 * form, `picker` the 32rem favorite and skin pickers, `wide` the 56rem armory. Without one, a
 * dialog is 46rem wide, the theme picker's width. A sheet takes the same name as a modifier
 * of its `hx-sheet` pane, which a variant styles when it needs to.
 */
export type DialogSize = 'form' | 'picker' | 'wide';
