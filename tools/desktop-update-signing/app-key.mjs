// Prints the public key compiled into the app (UpdateFeeds.FeedPublicKey), for the update
// gate, which checks that the build it installs trusts exactly this key:
//
//   node tools/desktop-update-signing/app-key.mjs
import { appPublicKey } from './lib/app-key.mjs';

try {
  process.stdout.write(`${appPublicKey()}\n`);
} catch (error) {
  console.error(`app-key: ${error.message}`);
  process.exitCode = 1;
}
