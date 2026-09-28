import { TestBed } from '@angular/core/testing';
import { AdminNotices } from '../../layout/admin-notices';

/** The band the last action left at the top of the admin, as `tone: text`; '' when none. */
export function notice(): string {
  const current = TestBed.inject(AdminNotices).current();
  return current === null ? '' : `${current.tone}: ${current.text}`;
}
