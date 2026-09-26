// Browser entry of the `web` build only (angular.json): the shell build keeps src/main.ts, as
// its hosts serve the pages themselves and must not be answered by a worker of the site.
import '../main';
import { registerWorker } from './registration/register-worker';

registerWorker(window);
