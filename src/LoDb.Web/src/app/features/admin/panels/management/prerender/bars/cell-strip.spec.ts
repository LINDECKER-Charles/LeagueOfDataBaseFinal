import { TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { CellStrip } from './cell-strip';

describe('CellStrip', () => {
  beforeEach(() => configureAdminTestBed());

  it('paints the cells across the whole width, one image named by its label', () => {
    const fixture = TestBed.createComponent(CellStrip);
    fixture.componentRef.setInput('cells', 'cf');
    fixture.componentRef.setInput('label', '1 langue complète sur 2');
    fixture.detectChanges();
    const strip = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role="img"]');

    expect(strip?.getAttribute('aria-label')).toBe('1 langue complète sur 2');
    expect(strip?.style.background).toContain('linear-gradient');
    expect(strip?.style.background).toContain('var(--color-bad) 50%');
  });
});
