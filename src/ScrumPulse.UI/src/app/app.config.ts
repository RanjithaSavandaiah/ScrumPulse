import { ApplicationConfig, isDevMode, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideStoreDevtools } from '@ngrx/store-devtools';
import { routes } from './app.routes';
import { demoInterceptor } from './demo/demo.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { sessionCredentialsInterceptor } from './core/interceptors/session-credentials.interceptor';
import { appEffects, appReducers } from './core/state';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      routes,

      // Binds route parameters straight to component inputs, so the blog article reads its
      // slug as an input rather than subscribing to ParamMap and needing to unsubscribe.
      withComponentInputBinding(),

      // Without this, following a link from halfway down the blog index opens the article
      // still scrolled halfway down.
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' })
    ),
    provideHttpClient(withFetch(), withInterceptors([demoInterceptor, sessionCredentialsInterceptor, errorInterceptor])),
    provideStore(appReducers),
    provideEffects(appEffects),
    provideStoreDevtools({
      maxAge: 25,
      logOnly: !isDevMode(),
      autoPause: true,
      trace: false,
      traceLimit: 75
    })
  ]
};
