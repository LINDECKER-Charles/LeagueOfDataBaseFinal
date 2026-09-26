import type { AndroidPlugins } from './android-plugins';

/**
 * One string kept in the secure storage under a fixed key. A failure of the Keystore is not
 * the caller's to handle: reading answers null, writing and erasing are best effort, so the
 * session still works for the life of the process.
 */
export class SecureItem {
  constructor(
    private readonly storage: AndroidPlugins['secureStorage'],
    private readonly key: string,
  ) {}

  async read(): Promise<string | null> {
    try {
      return await this.storage.getItem(this.key);
    } catch {
      return null;
    }
  }

  async write(value: string): Promise<void> {
    await this.storage.setItem(this.key, value).catch(() => undefined);
  }

  async erase(): Promise<void> {
    await this.storage.removeItem(this.key).catch(() => undefined);
  }
}
