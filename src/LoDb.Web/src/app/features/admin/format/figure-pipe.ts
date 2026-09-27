import { Pipe, type PipeTransform } from '@angular/core';
import { type FigureFormat, figure } from './figure';

/** `{{ bytes | figure: 'bytes' }}`: a figure written as {@link figure} does. */
@Pipe({ name: 'figure' })
export class FigurePipe implements PipeTransform {
  transform(value: number | null | undefined, format: FigureFormat = 'int'): string {
    return figure(value ?? 0, format);
  }
}
