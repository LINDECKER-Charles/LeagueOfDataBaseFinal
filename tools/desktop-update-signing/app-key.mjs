// Prints the public keys compiled into the app (UpdateFeeds.FeedPublicKeys), separated by
// spaces as the --smoke report lists them, for the update gate, which checks that the build it
// installs trusts exactly these keys:
//
//   node tools/desktop-update-signing/app-key.mjs
import { appPublicKeys, keyList } from './lib/app-key.mjs';

try {
  process.stdout.write(`${keyList(appPublicKeys())}\n`);
} catch (error) {
  console.error(`app-key: ${error.message}`);
  process.exitCode = 1;
}
