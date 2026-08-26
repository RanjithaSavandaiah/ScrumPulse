import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { PullRequestActions } from './pull-requests.actions';
import { DeveloperPrMetrics, PullRequestLog } from '../../models/scrum.models';

@Injectable()
export class PullRequestsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadPullRequests$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PullRequestActions.loadPullRequests),
      switchMap(({ sprintId }) => {
        const url = sprintId ? `${this.apiUrl}/pullrequests?sprintId=${sprintId}` : `${this.apiUrl}/pullrequests`;
        return this.http.get<PullRequestLog[]>(url).pipe(
          map(prLogs => PullRequestActions.loadPullRequestsSuccess({ prLogs })),
          catchError(err => of(PullRequestActions.loadPullRequestsFailure({ error: err.message })))
        );
      })
    )
  );

  loadDeveloperPrMetrics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PullRequestActions.loadDeveloperPrMetrics),
      switchMap(({ sprintId }) => {
        const url = sprintId
          ? `${this.apiUrl}/pullrequests/developer-metrics?sprintId=${sprintId}`
          : `${this.apiUrl}/pullrequests/developer-metrics`;
        return this.http.get<DeveloperPrMetrics[]>(url).pipe(
          map(metrics => PullRequestActions.loadDeveloperPrMetricsSuccess({ metrics })),
          catchError(err => of(PullRequestActions.loadDeveloperPrMetricsFailure({ error: err.message })))
        );
      })
    )
  );

  createPullRequestLog$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PullRequestActions.createPullRequestLog),
      mergeMap(({ request }) =>
        this.http.post<PullRequestLog>(`${this.apiUrl}/pullrequests`, request).pipe(
          map(created => PullRequestActions.createPullRequestLogSuccess({ log: created })),
          catchError(() => of())
        )
      )
    )
  );

  deletePullRequestLog$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PullRequestActions.deletePullRequestLog),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/pullrequests/${id}`).pipe(
          map(() => PullRequestActions.deletePullRequestLogSuccess({ id })),
          catchError(() => of())
        )
      )
    )
  );
}
