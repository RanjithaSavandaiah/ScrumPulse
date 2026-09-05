import { Component, Input, AfterViewInit, inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-ad-banner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ad-banner.component.html',
  styleUrl: './ad-banner.component.css'
})
export class AdBannerComponent implements AfterViewInit {
  private platformId = inject(PLATFORM_ID);

  @Input() publisherId: string = 'ca-pub-1773214213114642';
  @Input() adSlot?: string;
  @Input() adFormat: string = 'auto';
  @Input() fullWidthResponsive: boolean = true;
  @Input() showLabel: boolean = true;

  ngAfterViewInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      try {
        const adsbygoogle = (window as any).adsbygoogle || [];
        adsbygoogle.push({});
      } catch (err) {
        console.warn('[AdBannerComponent] Failed to initialize Google AdSense slot:', err);
      }
    }
  }
}
