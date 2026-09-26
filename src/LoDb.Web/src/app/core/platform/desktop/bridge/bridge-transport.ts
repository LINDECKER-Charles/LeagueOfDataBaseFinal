/**
 * The messaging the desktop host injects with its marker (`window.__LODB_DESKTOP__.bridge`,
 * spike report 6.2): strings both ways, over `window.external` under Photino. Only this
 * transport is known here, so that a change of WebView only changes the host's line.
 */
export interface BridgeTransport {
  send(message: string): void;
  listen(listener: (message: string) => void): void;
}
