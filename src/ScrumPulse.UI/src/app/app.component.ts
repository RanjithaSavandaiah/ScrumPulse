import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmationPopupComponent } from './core/components/confirmation-popup/confirmation-popup.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ConfirmationPopupComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {}
