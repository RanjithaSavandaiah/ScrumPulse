import { Pipe, PipeTransform } from '@angular/core';
import { getBadgeLabel } from '../utils/format-utils';

@Pipe({
  name: 'badgeLabel',
  standalone: true
})
export class BadgeLabelPipe implements PipeTransform {
  transform(badge?: string | number | null): string {
    return getBadgeLabel(badge);
  }
}
