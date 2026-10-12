import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTestTransloco } from '../../../../core/testing/provide-test-transloco';
import { PurchaseOrder } from '../steps/order/purchase-order';
import type { ArmoryData } from './armory-data';
import { ITEM_CATEGORIES } from './item-categories';
import { ItemArmory } from './item-armory';

const LABELS = { build: { editor: { armory: { done: 'Done' } } } };
const LONG_SWORD = {
  id: '1036',
  name: 'Long Sword',
  gold: 350,
  tags: ['Damage'],
  image: { status: 'absent' as const },
};

async function mount(): Promise<{ fixture: ComponentFixture<ItemArmory>; close: () => void }> {
  const close = vi.fn();
  const data = {
    step: 0,
    editing: { order: signal(PurchaseOrder.of([{ label: 'Core', items: [] }])) },
    catalogs: {
      items: { options: signal([LONG_SWORD]), status: signal('ready'), retry: vi.fn() },
    },
  } as unknown as ArmoryData;
  TestBed.configureTestingModule({
    providers: [
      { provide: DialogRef, useValue: { close } },
      { provide: DIALOG_DATA, useValue: data },
      provideTestTransloco({ otherwise: LABELS }),
    ],
  });
  const fixture = TestBed.createComponent(ItemArmory);
  await fixture.whenStable();
  return { fixture, close };
}

describe('ItemArmory', () => {
  it('opens on every item of the patch, its first chip pressed', async () => {
    const { fixture } = await mount();
    const armory = fixture.nativeElement as HTMLElement;
    const chips = [...armory.querySelectorAll('.armory__cat')];

    expect(chips.map((chip) => chip.getAttribute('aria-pressed'))).toEqual(
      ITEM_CATEGORIES.map((category) => String(category === 'all')),
    );
    expect(armory.querySelectorAll('lodb-armory-item-tile')).toHaveLength(1);
  });

  it('closes on Done', async () => {
    const { fixture, close } = await mount();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.armory__done')
      ?.click();

    expect(close).toHaveBeenCalledOnce();
  });
});
