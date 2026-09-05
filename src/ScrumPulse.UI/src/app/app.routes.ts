import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { PrivacyPolicyComponent } from './legal/privacy-policy/privacy-policy.component';
import { TermsComponent } from './legal/terms/terms.component';

export const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'privacy-policy', component: PrivacyPolicyComponent },
  { path: 'privacy', redirectTo: 'privacy-policy', pathMatch: 'full' },
  { path: 'terms', component: TermsComponent },
  { path: 'terms-of-service', redirectTo: 'terms', pathMatch: 'full' },
  { path: '**', redirectTo: '' }
];
