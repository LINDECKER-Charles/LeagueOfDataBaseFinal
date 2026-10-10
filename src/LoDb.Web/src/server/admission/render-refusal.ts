/**
 * Why the admission refused a render, the `reason` of `lodb_ssr_renders_total`: the server,
 * the lane or the client already runs as many renders as it may (`*capacity`), or the lane
 * or the client has spent its renders of the minute (`*_rate`).
 */
export type RenderRefusal =
  'capacity' | 'lane_capacity' | 'client_capacity' | 'lane_rate' | 'client_rate';
