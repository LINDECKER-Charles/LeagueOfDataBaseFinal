import { environment as web } from './environment';
import { environment as shell } from './environment.shell';
import { environment as store } from './environment.store';

describe('build environments', () => {
  it('lets the web and the desktop shell show payments', () => {
    expect(web.payments).toBe(true);
    expect(shell.payments).toBe(true);
  });

  it('removes every payment from the store build of the apps', () => {
    expect(store.payments).toBe(false);
  });

  it('points the store build at the same public API as the shell', () => {
    expect(store.publicApiOrigin).toBe(shell.publicApiOrigin);
    expect(store.publicApiOrigin).toMatch(/^https:\/\/[^/]+$/);
  });

  it('keeps the web build on the origin of its pages', () => {
    expect(web.publicApiOrigin).toBeNull();
  });
});
