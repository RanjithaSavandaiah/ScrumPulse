import { Pipe, PipeTransform } from '@angular/core';
import { getInitials } from '../utils/format-utils';

@Pipe({
  name: 'initials',
  standalone: true
})
export class InitialsPipe implements PipeTransform {
  transform(name?: string | null): string {
    return getInitials(name);
  }
}
