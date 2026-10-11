import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { checkFeed, feedFileName, feedOfVersion } from '../lib/release-feed.mjs';
import { testEntry, testFeed } from './test-keys.mjs';

describe('release feed', () => {
  it('names the feed of a Velopack channel as Velopack does', () => {
    assert.equal(feedFileName('win-x64'), 'releases.win-x64.json');
    assert.equal(feedFileName('osx-arm64-beta'), 'releases.osx-arm64-beta.json');
  });

  it('refuses a channel that would name another file', () => {
    for (const channel of ['', 'win', '../win-x64', 'win-x64/..', 'Win-X64']) {
      assert.throws(() => feedFileName(channel), /invalid Velopack channel/, channel);
    }
  });

  it('keeps the packages of the release, as its GitHub Release holds them', () => {
    const previous = testEntry('1.3.0');
    const own = [testEntry('1.4.0'), testEntry('1.4.0', 'Delta')];

    const published = JSON.parse(feedOfVersion(testFeed([previous, ...own]), '1.4.0'));

    assert.deepEqual(published, { Assets: own });
  });

  it('refuses a feed without a package of the version', () => {
    assert.throws(
      () => feedOfVersion(testFeed([testEntry('1.3.0')]), '1.4.0'),
      /no package of version 1.4.0/,
    );
  });

  it('refuses an entry the app would refuse', () => {
    const cases = [
      [{ SHA256: undefined }, /no SHA-256/],
      [{ SHA256: 'ABCDEF' }, /no SHA-256/],
      [{ Size: 0 }, /no size/],
      [{ FileName: '../LoDb.Desktop-1.4.0-win-x64-full.nupkg' }, /invalid package name/],
      [{ Version: '' }, /no version/],
    ];
    for (const [change, error] of cases) {
      const feed = testFeed([{ ...testEntry('1.4.0'), ...change }]);

      assert.throws(() => checkFeed(feed), error, JSON.stringify(change));
    }
  });

  // The app matches key names without case and keeps the last of two: "sha256" would win.
  it('refuses a key the app could read otherwise than these checks', () => {
    const other = 'A'.repeat(64);
    for (const change of [{ sha256: other }, { Sha256: other }, { size: 1 }, { Extra: '' }]) {
      const feed = testFeed([{ ...testEntry('1.4.0'), ...change }]);

      assert.throws(() => checkFeed(feed), /unexpected keys/, JSON.stringify(change));
    }
    const twoLists = { Assets: [testEntry('1.4.0')], assets: [testEntry('1.5.0')] };
    assert.throws(
      () => checkFeed(Buffer.from(JSON.stringify(twoLists))),
      /the feed holds unexpected keys: "assets"/,
    );
  });

  it('refuses a feed that lists nothing', () => {
    assert.throws(() => checkFeed(Buffer.from('{"Assets":[]}')), /lists no package/);
    assert.throws(() => checkFeed(Buffer.from('null')), /lists no package/);
  });
});
