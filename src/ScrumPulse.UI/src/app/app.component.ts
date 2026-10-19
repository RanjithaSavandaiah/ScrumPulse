import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmationPopupComponent } from './core/components/confirmation-popup/confirmation-popup.component';
import { DemoBannerComponent } from './demo/demo-banner.component';
import { VoiceCaptureBarComponent } from './core/components/voice-capture-bar/voice-capture-bar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    ConfirmationPopupComponent,
    DemoBannerComponent,
    VoiceCaptureBarComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {}
