import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../../core/components/icon/icon.component';
import { FooterComponent } from '../../core/components/footer/footer.component';
import { AdBannerComponent } from '../../core/components/ad-banner/ad-banner.component';

@Component({
  selector: 'app-privacy-policy',
  standalone: true,
  imports: [CommonModule, RouterLink, IconComponent, FooterComponent, AdBannerComponent],
  templateUrl: './privacy-policy.component.html',
  styleUrl: './privacy-policy.component.css'
})
export class PrivacyPolicyComponent {
  lastUpdated = 'September 5, 2026';
}
