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
        const isTestEnv = typeof window !== 'undefined' && ((window as any).__karma__ || (window as any).jasmine);
        if (!isTestEnv && !document.querySelector('script[src*="adsbygoogle.js"]')) {
          const script = document.createElement('script');
          script.async = true;
          script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${this.publisherId}`;
          script.crossOrigin = 'anonymous';
          document.head.appendChild(script);
        }
        try {
          const adsbygoogle = (window as any).adsbygoogle || [];
          adsbygoogle.push({});
          (window as any).adsbygoogle = adsbygoogle;
        } catch (pushError) {
          console.info('[AdBannerComponent] Note: Ad slot push skipped or already filled:', pushError);
        }
      } catch (initError) {
        console.info('[AdBannerComponent] Note: AdSense initialization bypassed or blocked by client:', initError);
      }
    }
  }
}
