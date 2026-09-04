import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches the active tenant team identifier (X-Team-Id) and user identity (X-User-Role, X-User-Name)
 * to outgoing HTTP requests ensuring squad-level multi-tenant isolation and accurate audit trails.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const teamId = localStorage.getItem('scrumpulse_current_team_id');
  const role = localStorage.getItem('scrumpulse_current_role') || 'Scrum Master';
  const roleFormatted = role === 'ScrumMaster' ? 'Scrum Master' : (role === 'QaEngineer' ? 'QA Engineer' : role);

  const headers: Record<string, string> = {
    'X-User-Role': roleFormatted,
    'X-User-Name': roleFormatted
  };

  if (teamId && teamId.trim().length > 0) {
    headers['X-Team-Id'] = teamId.trim();
  }

  const cloned = req.clone({ setHeaders: headers });
  return next(cloned);
};
