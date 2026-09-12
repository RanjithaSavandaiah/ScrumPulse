import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'privacy-policy',
    loadComponent: () => import('./legal/privacy-policy/privacy-policy.component').then(m => m.PrivacyPolicyComponent)
  },
  { path: 'privacy', redirectTo: 'privacy-policy', pathMatch: 'full' },
  {
    path: 'terms',
    loadComponent: () => import('./legal/terms/terms.component').then(m => m.TermsComponent)
  },
  { path: 'terms-of-service', redirectTo: 'terms', pathMatch: 'full' },
  { path: '**', redirectTo: '' }
];
