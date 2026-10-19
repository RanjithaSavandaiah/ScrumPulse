import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Sends the session cookie with every API request.
 *
 * This replaces an interceptor that attached X-Team-Id, X-User-Role and X-User-Name headers
 * read from local storage. Those headers were how the server identified the caller's squad
 * and role, which meant anyone could edit local storage and become a Scrum Master on any
 * squad. Identity now travels in an HttpOnly cookie that page script cannot read or forge.
 *
 * `withCredentials` is required because Angular's HttpClient omits cookies by default on
 * cross-origin requests, and during development the SPA runs on port 4200 while the API runs
 * elsewhere. Without it the browser holds a valid session and never presents it, which is the
 * failure that led to the header fallbacks being reintroduced server side.
 *
 * Only same-origin API calls are touched. Attaching credentials to a third-party request
 * would leak the session cookie to whoever that request addresses.
 */
export const sessionCredentialsInterceptor: HttpInterceptorFn = (request, next) => {
  const isApiRequest = request.url.startsWith('/api/') || request.url.includes('/api/');

  if (!isApiRequest) {
    return next(request);
  }

  return next(request.clone({ withCredentials: true }));
};
