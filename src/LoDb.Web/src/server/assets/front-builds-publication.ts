import type { Logger } from 'pino';
import { publishFrontBuilds } from './publish-front-builds';
import type { PublishedBuild } from './published-build';

// The deploy job purges the shared folder by date, in whole days (`find -mtime`): refreshed
// every hour, the files of the running build are never more than an hour old when the next
// deploy replaces it, however long it has run.
const REFRESH_INTERVAL_MINUTES = 60;
const MS_PER_MINUTE = 60_000;

/**
 * Keeps this build's hashed files published in the folder every build shares
 * (`publishFrontBuilds`): at start, in the background, then every hour for as long as the
 * server runs. The date of a file then tells the deploy job's purge when a running build last
 * shipped it, not when that build started: the build a deploy replaces keeps its files for
 * the whole retention, for the tabs and the cached pages that still name them.
 */
export class FrontBuildsPublication {
  constructor(
    private readonly browserFolder: string,
    private readonly sharedFolder: string,
    private readonly logger: Logger,
  ) {}

  /** Publishes now, logging what it did, then every hour without keeping the process alive. */
  start(): void {
    void this.publish().then((published) => {
      if (published !== null) {
        this.logger.info(published, 'ssr.builds.published');
      }
    });
    setInterval(() => void this.publish(), REFRESH_INTERVAL_MINUTES * MS_PER_MINUTE).unref();
  }

  // Logged, never thrown: the image's own folder serves this build meanwhile.
  private async publish(): Promise<PublishedBuild | null> {
    try {
      return await publishFrontBuilds(this.browserFolder, this.sharedFolder, new Date());
    } catch (error: unknown) {
      this.logger.error({ exception: error }, 'ssr.builds.failed');
      return null;
    }
  }
}
