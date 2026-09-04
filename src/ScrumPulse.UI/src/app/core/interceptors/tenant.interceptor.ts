import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches the active tenant team identifier (X-Team-Id) to outgoing HTTP requests
 * ensuring squad-level multi-tenant isolation.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const teamId = localStorage.getItem('scrumpulse_current_team_id');
  if (teamId && teamId.trim().length > 0) {
    const cloned = req.clone({
      setHeaders: {
        'X-Team-Id': teamId.trim()
      }
    });
    return next(cloned);
  }
  return next(req);
};
