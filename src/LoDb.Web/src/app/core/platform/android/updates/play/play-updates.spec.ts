import { TestBed } from '@angular/core/testing';
import {
  AppUpdateAvailability,
  AppUpdateResultCode,
  FlexibleUpdateInstallStatus,
} from '@capawesome/capacitor-app-update';
import { UPDATE_PLUGINS } from '../native/update-plugins-token';
import { FakeAppUpdate } from '../testing/fake-app-update';
import { FakeLiveUpdate } from '../testing/fake-live-update';
import { PlayUpdates } from './play-updates';

describe('PlayUpdates', () => {
  let play: FakeAppUpdate;

  function updates(): PlayUpdates {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UPDATE_PLUGINS,
          useValue: { liveUpdate: new FakeLiveUpdate().plugin, appUpdate: play.plugin },
        },
      ],
    });
    return TestBed.inject(PlayUpdates);
  }

  beforeEach(() => {
    play = new FakeAppUpdate();
  });

  it('hands over to the transitional channel for an installation outside Play', async () => {
    play.unmanaged = true;

    expect(await updates().run('flexible')).toBe(false);
  });

  it('hands over when Play knows nothing of the installation', async () => {
    play.info = { ...play.info, updateAvailability: AppUpdateAvailability.UNKNOWN };

    expect(await updates().run('immediate')).toBe(false);
  });

  describe('immediate', () => {
    it('lets Play cover the app until the new version runs', async () => {
      play.offer('2004000');

      expect(await updates().run('immediate')).toBe(true);
      expect(play.immediateUpdates).toBe(1);
      expect(play.flexibleUpdates).toBe(0);
    });

    it('resumes an immediate update the user left', async () => {
      play.offer('2004000', { updateAvailability: AppUpdateAvailability.UPDATE_IN_PROGRESS });

      await updates().run('immediate');

      expect(play.immediateUpdates).toBe(1);
    });

    it('opens the store listing when Play refuses the immediate flow', async () => {
      play.offer('2004000', { immediateUpdateAllowed: false });

      await updates().run('immediate');

      expect(play.immediateUpdates).toBe(0);
      expect(play.storeOpenings).toBe(1);
    });

    it('waits for Play to serve the new version to this device', async () => {
      expect(await updates().run('immediate')).toBe(true);
      expect(play.immediateUpdates + play.storeOpenings).toBe(0);
    });
  });

  describe('flexible', () => {
    it('offers the update once per version, then follows its download', async () => {
      play.offer('2004000');
      const service = updates();

      await service.run('flexible');
      await service.run('flexible');

      expect(play.flexibleUpdates).toBe(1);
      expect(service.state()).toBe('downloading');
      play.emit({
        installStatus: FlexibleUpdateInstallStatus.DOWNLOADED,
        bytesDownloaded: undefined,
        totalBytesToDownload: undefined,
      });
      expect(service.state()).toBe('ready');
    });

    it('offers a newer version again after a refusal', async () => {
      play.offer('2004000');
      play.flexibleAnswer = AppUpdateResultCode.CANCELED;
      const service = updates();
      await service.run('flexible');
      expect(service.state()).toBe('none');

      play.offer('2005000');
      await service.run('flexible');

      expect(play.flexibleUpdates).toBe(2);
    });

    it('never offers what Play does not allow in the flexible flow', async () => {
      play.offer('2004000', { flexibleUpdateAllowed: false });

      await updates().run('flexible');

      expect(play.flexibleUpdates).toBe(0);
    });

    it('is ready at once for an update downloaded during an earlier session', async () => {
      play.offer('2004000', { installStatus: FlexibleUpdateInstallStatus.DOWNLOADED });
      const service = updates();

      await service.run('none');

      expect(service.state()).toBe('ready');
      await service.apply();
      expect(play.completions).toBe(1);
    });

    it('shows nothing once a download failed', async () => {
      play.offer('2004000');
      const service = updates();
      await service.run('flexible');

      play.emit({
        installStatus: FlexibleUpdateInstallStatus.FAILED,
        bytesDownloaded: undefined,
        totalBytesToDownload: undefined,
      });

      expect(service.state()).toBe('none');
    });
  });
});
