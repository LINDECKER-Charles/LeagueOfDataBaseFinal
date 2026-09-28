import { TestBed } from '@angular/core/testing';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { AndroidLifecycle } from './android-lifecycle';

describe('AndroidLifecycle', () => {
  it('reports each return to the foreground, listening once', () => {
    const native = new FakeAndroidPlugins();
    TestBed.configureTestingModule({
      providers: [{ provide: ANDROID_PLUGINS, useValue: native.plugins }],
    });
    const lifecycle = TestBed.inject(AndroidLifecycle);
    let resumes = 0;
    lifecycle.resumes.subscribe(() => resumes++);

    lifecycle.start();
    lifecycle.start();
    native.emit('resume');
    native.emit('resume');

    expect(native.listeners.get('resume')).toHaveLength(1);
    expect(resumes).toBe(2);
  });
});
