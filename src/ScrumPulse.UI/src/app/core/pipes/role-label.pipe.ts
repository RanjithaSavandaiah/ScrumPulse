import { Pipe, PipeTransform } from '@angular/core';
import { getRoleLabel } from '../utils/format-utils';

@Pipe({
  name: 'roleLabel',
  standalone: true
})
export class RoleLabelPipe implements PipeTransform {
  transform(role?: string | number | null): string {
    return getRoleLabel(role);
  }
}
