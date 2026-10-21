/** A render the admission decides on: what it renders and on whose behalf. */
export interface RenderRequest {
  /** Root-relative URL of the page, as requested. */
  readonly url: string;
  /** The client the render counts against (`clientKeyOf`), never logged. */
  readonly client: string;
  /** Whether the pre-render pass asks for it (`isPrerenderRender`): the `prerender` lane. */
  readonly isPrerender: boolean;
}
