import { Pipe, PipeTransform } from '@angular/core';
import { cleanName } from '../utils/format-utils';

@Pipe({
  name: 'cleanName',
  standalone: true
})
export class CleanNamePipe implements PipeTransform {
  transform(value?: string | null): string {
    return cleanName(value);
  }
}
