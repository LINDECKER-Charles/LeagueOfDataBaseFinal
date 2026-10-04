import { Pipe, type PipeTransform } from '@angular/core';
import { type StampFormat, stamp } from './stamp';

/** `{{ row.createdAt | stamp: 'minute' }}`: an instant written as {@link stamp} does. */
@Pipe({ name: 'stamp' })
export class StampPipe implements PipeTransform {
  transform(iso: string | null | undefined, format: StampFormat = 'date'): string {
    return stamp(iso, format);
  }
}
