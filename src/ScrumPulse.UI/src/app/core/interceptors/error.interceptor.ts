import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'An unexpected network error occurred.';

      if (error.error instanceof ErrorEvent) {
        // Client-side / network error
        errorMessage = `Client Error: ${error.error.message}`;
      } else {
        // Server-side RFC 7807 problem details or standard error
        if (error.error?.detail) {
          errorMessage = error.error.detail;
        } else if (error.error?.title) {
          errorMessage = error.error.title;
        } else if (error.status === 0) {
          errorMessage = 'Unable to connect to ScrumPulse API server. Please check your internet connection.';
        } else {
          errorMessage = `HTTP ${error.status}: ${error.statusText || 'Server Error'}`;
        }
      }

      console.error(`[ScrumPulse HTTP Error] [${req.method} ${req.url}]:`, errorMessage, error);

      return throwError(() => new Error(errorMessage));
    })
  );
};
