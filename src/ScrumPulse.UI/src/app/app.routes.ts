import { Routes } from '@angular/router';

/**
 * Public marketing routes sit at the root; the application itself lives under /app.
 *
 * The root previously served the dashboard, so a first-time visitor - and every crawler -
 * landed on a squad view that showed nothing without a join code. That is a poor introduction
 * and the reason the site read as having no content at all. The tool is unchanged and simply
 * moved; /dashboard redirects so existing links and bookmarks keep working.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./marketing/landing/landing.component').then((m) => m.LandingComponent)
  },
  {
    path: 'about',
    loadComponent: () => import('./marketing/about/about.component').then((m) => m.AboutComponent)
  },
  {
    path: 'contact',
    loadComponent: () => import('./marketing/contact/contact.component').then((m) => m.ContactComponent)
  },
  {
    path: 'blog',
    loadComponent: () => import('./marketing/blog/blog-list.component').then((m) => m.BlogListComponent)
  },
  {
    // The slug binds to the component's `slug` input via withComponentInputBinding(),
    // so the article component needs no route subscription to clean up.
    path: 'blog/:slug',
    loadComponent: () => import('./marketing/blog/blog-post.component').then((m) => m.BlogPostComponent)
  },

  {
    path: 'privacy-policy',
    loadComponent: () =>
      import('./legal/privacy-policy/privacy-policy.component').then((m) => m.PrivacyPolicyComponent)
  },
  { path: 'privacy', redirectTo: 'privacy-policy', pathMatch: 'full' },
  {
    path: 'terms',
    loadComponent: () => import('./legal/terms/terms.component').then((m) => m.TermsComponent)
  },
  { path: 'terms-of-service', redirectTo: 'terms', pathMatch: 'full' },

  // Enables fixture mode then forwards to the application, so a visitor sees a populated
  // product without a join code and without any real squad's data being exposed.
  {
    path: 'demo',
    loadComponent: () => import('./demo/demo-entry.component').then((m) => m.DemoEntryComponent)
  },

  // Declared ahead of 'app', which matches by prefix. Reversing these two leaves the board
  // screen unreachable, and the only symptom is the dashboard appearing instead.
  {
    path: 'app/board-connection',
    loadComponent: () =>
      import('./features/board-connection/board-connection.component').then(
        (m) => m.BoardConnectionComponent
      )
  },
  {
    path: 'app',
    loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent)
  },

  // The dashboard used to live at the root. Existing links and bookmarks still point here,
  // so the path is kept as a redirect rather than removed.
  { path: 'dashboard', redirectTo: 'app', pathMatch: 'full' },

  // Anything unrecognised goes to the marketing page rather than a blank screen, which also
  // keeps a crawler hitting a stale URL on a real page instead of an empty shell.
  { path: '**', redirectTo: '' }
];
