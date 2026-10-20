import { Pipe, type PipeTransform } from '@angular/core';
import { decimalBytes } from './decimal-bytes';

/** `{{ bytes | decimalBytes }}`: a volume written as {@link decimalBytes} does. */
@Pipe({ name: 'decimalBytes' })
export class DecimalBytesPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return decimalBytes(value ?? 0);
  }
}
