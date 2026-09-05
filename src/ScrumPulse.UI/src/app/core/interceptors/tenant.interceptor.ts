import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Attaches the active tenant team identifier (X-Team-Id) and user identity (X-User-Role, X-User-Name)
 * to outgoing HTTP requests ensuring squad-level multi-tenant isolation and accurate audit trails.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  let teamId: string | null = null;
  let role = 'ScrumMaster';
  try {
    teamId = localStorage.getItem('scrumpulse_current_team_id');
    role = localStorage.getItem('scrumpulse_current_role') || 'ScrumMaster';
  } catch (err) {
    console.warn('[tenantInterceptor] Failed to read tenant/role from localStorage:', err);
  }

  const headers: Record<string, string> = {
    'X-User-Role': role,
    'X-User-Name': role
  };

  if (teamId && teamId.trim().length > 0) {
    headers['X-Team-Id'] = teamId.trim();
  }

  const cloned = req.clone({ setHeaders: headers });
  return next(cloned);
};
