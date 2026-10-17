import { ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';

/**
 * Configuration used only while prerendering.
 *
 * Merged over the browser config, so the public pages are generated with the same providers,
 * router and store the browser uses. Anything registered here alone would exist in the static
 * HTML and then vanish on hydration, which is the usual source of content flickering away
 * immediately after a prerendered page loads.
 */
const serverConfig: ApplicationConfig = {
  providers: [provideServerRendering(withRoutes(serverRoutes))]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
