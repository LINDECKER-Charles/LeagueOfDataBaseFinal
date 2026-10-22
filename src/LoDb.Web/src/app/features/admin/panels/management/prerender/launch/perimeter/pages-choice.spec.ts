import { TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { PagesChoice } from './pages-choice';

function render(isOpenersFirst: boolean) {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(PagesChoice);
  fixture.componentRef.setInput('isOpenersFirst', isOpenersFirst);
  fixture.detectChanges();
  const changes: boolean[] = [];
  fixture.componentInstance.changed.subscribe((value) => changes.push(value));
  return { host: fixture.nativeElement as HTMLElement, changes };
}

function radio(host: HTMLElement, key: string): HTMLInputElement {
  return host.querySelector(`[data-pages="${key}"] input`) as HTMLInputElement;
}

describe('PagesChoice', () => {
  it('checks every page by default and says nothing more', () => {
    const { host } = render(false);

    expect([radio(host, 'every').checked, radio(host, 'openers').checked]).toEqual([true, false]);
    expect(host.querySelector('[data-pages-openers]')).toBeNull();
  });

  it('emits the openers once picked, and notes what they leave to the next pass', () => {
    const { host, changes } = render(true);

    radio(host, 'openers').dispatchEvent(new Event('change'));

    expect(changes).toEqual([true]);
    expect(host.querySelector('[data-pages-openers]')).not.toBeNull();
  });
});
