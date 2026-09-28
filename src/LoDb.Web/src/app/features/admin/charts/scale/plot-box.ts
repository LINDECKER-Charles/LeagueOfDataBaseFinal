/** The SVG user space of a time-series chart and the plot drawn inside its margins. */
export interface PlotBox {
  readonly w: number;
  readonly h: number;
  /** Left and right margin: the Y labels on the left, the last X label on the right. */
  readonly padX: number;
  readonly padTop: number;
  readonly plotW: number;
  readonly plotH: number;
}

const W = 760;
const H = 240;
const PAD_X = 34;
const PAD_TOP = 16;
const PAD_BOTTOM = 26;

/**
 * The legacy canvas (SvgPrimitives): the charts scale to their container through the
 * viewBox, so every mark is placed in these fixed units.
 */
export const PLOT_BOX: PlotBox = {
  w: W,
  h: H,
  padX: PAD_X,
  padTop: PAD_TOP,
  plotW: W - 2 * PAD_X,
  plotH: H - PAD_TOP - PAD_BOTTOM,
};
