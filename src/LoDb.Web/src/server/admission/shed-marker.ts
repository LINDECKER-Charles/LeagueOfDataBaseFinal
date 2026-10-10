/**
 * Key of `response.locals` the admission sets on a response it refused: a shed render is
 * counted in the minute summary (`AdmissionSummary`), so the request log skips it.
 */
export const SHED_MARKER = 'lodbShedRender';
