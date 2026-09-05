import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type IconName =
  | 'zap'
  | 'clock'
  | 'bot'
  | 'shield-alert'
  | 'users'
  | 'calendar'
  | 'file-text'
  | 'rotate-cw'
  | 'award'
  | 'book-open'
  | 'bar-chart'
  | 'git-pull-request'
  | 'git-merge'
  | 'git-branch'
  | 'check-circle'
  | 'shield-check'
  | 'eye'
  | 'play'
  | 'pause'
  | 'rotate-ccw'
  | 'arrow-right'
  | 'flask'
  | 'plus'
  | 'alert-triangle'
  | 'alert-circle'
  | 'sparkles'
  | 'user'
  | 'target'
  | 'building'
  | 'send'
  | 'message-square'
  | 'message-circle'
  | 'sun'
  | 'moon'
  | 'refresh-cw'
  | 'thumbs-up'
  | 'smile'
  | 'frown'
  | 'lightbulb'
  | 'check-square'
  | 'heart'
  | 'rocket'
  | 'shield'
  | 'star'
  | 'gift'
  | 'trophy'
  | 'wrench'
  | 'mic'
  | 'copy'
  | 'download'
  | 'check'
  | 'user-check'
  | 'briefcase'
  | 'palmtree'
  | 'edit-3'
  | 'edit'
  | 'trash-2'
  | 'trash'
  | 'x'
  | 'external-link'
  | 'eye-off'
  | 'activity'
  | 'loader'
  | 'trending-up'
  | 'trending-down'
  | 'minus';

@Component({
  selector: 'app-icon',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.css'
})
export class IconComponent {
  @Input() name: IconName = 'zap';
  @Input() size: number = 18;
  @Input() color?: string;
  @Input() strokeWidth: number = 2;
  @Input() extraClass: string = '';
}
