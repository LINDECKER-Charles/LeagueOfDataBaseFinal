// The WebView of the app, through the Chrome DevTools Protocol: a debug build exposes it on
// the abstract socket webview_devtools_remote_<pid>, which adb forwards to the host.

/** The WebView DevTools socket of the process, from /proc/net/unix; null when absent. */
export function devtoolsSocketOf(procNetUnix, pid) {
  const name = `webview_devtools_remote_${pid}`;
  const found = procNetUnix.split('\n').some((line) => line.trim().endsWith(`@${name}`));
  return found ? name : null;
}

/** The page of the app among the DevTools targets: served by Capacitor from localhost. */
export function appPageOf(targets) {
  return (
    targets.find(
      (target) =>
        target.type === 'page' &&
        typeof target.webSocketDebuggerUrl === 'string' &&
        String(target.url).startsWith('https://localhost'),
    ) ?? null
  );
}

/** The value of a `Runtime.evaluate` answer, or its exception as an error. */
export function evaluationResult(response) {
  if (response.error) {
    throw new Error(`DevTools: ${response.error.message}`);
  }
  const { result, exceptionDetails } = response.result;
  if (exceptionDetails) {
    const description = exceptionDetails.exception?.description ?? exceptionDetails.text;
    throw new Error(description);
  }
  return result.value;
}
