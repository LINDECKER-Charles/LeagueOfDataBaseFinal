import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccountHead } from './account-head';
import { overrideAccountTitle } from './override-account-title';

// The name the view has loaded; null while it loads.
const summoner = signal<string | null>(null);

@Component({
  selector: 'lodb-titled-view',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class TitledView {
  constructor() {
    overrideAccountTitle(() => {
      const name = summoner();
      return name === null ? null : { text: name };
    });
  }
}

@Component({
  imports: [TitledView],
  template: `@if (shown()) {
    <lodb-titled-view />
  }`,
  providers: [AccountHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class AccountPageHost {
  readonly head = inject(AccountHead);
  readonly shown = signal(true);
}

describe('overrideAccountTitle', () => {
  afterEach(() => summoner.set(null));

  it('names the page by the view, then gives the route its title back', async () => {
    const fixture = TestBed.createComponent(AccountPageHost);
    const { head, shown } = fixture.componentInstance;
    await fixture.whenStable();
    expect(head.override()).toBeNull();

    summoner.set('Faker#KR1');
    await fixture.whenStable();
    expect(head.override()).toEqual({ text: 'Faker#KR1' });

    shown.set(false);
    await fixture.whenStable();
    expect(head.override()).toBeNull();
  });
});
