import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { WorkItemActions } from './work-items.actions';
import { WorkItem } from '../../models/scrum.models';

@Injectable()
export class WorkItemsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadWorkItems$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.loadWorkItems),
      switchMap(({ sprintId }) => {
        const url = sprintId ? `${this.apiUrl}/workitems?sprintId=${sprintId}` : `${this.apiUrl}/workitems`;
        return this.http.get<WorkItem[]>(url).pipe(
          map(items => WorkItemActions.loadWorkItemsSuccess({ items })),
          catchError(err => of(WorkItemActions.loadWorkItemsFailure({ error: err.message })))
        );
      })
    )
  );

  createWorkItem$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.createWorkItem),
      mergeMap(({ item }) =>
        this.http.post<WorkItem>(`${this.apiUrl}/workitems`, item).pipe(
          map(created => WorkItemActions.createWorkItemSuccess({ item: created })),
          catchError(err => {
            console.error('[WorkItemsEffects] Failed to create work item:', err);
            return of();
          })
        )
      )
    )
  );

  updateWorkItem$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.updateWorkItem),
      mergeMap(({ id, item }) =>
        this.http.put<WorkItem>(`${this.apiUrl}/workitems/${id}`, item).pipe(
          map(updated => WorkItemActions.updateWorkItemSuccess({ item: updated })),
          catchError(err => {
            console.error('[WorkItemsEffects] Failed to update work item:', err);
            return of();
          })
        )
      )
    )
  );

  deleteWorkItem$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.deleteWorkItem),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/workitems/${id}`).pipe(
          map(() => WorkItemActions.deleteWorkItemSuccess({ id })),
          catchError(err => {
            console.error('[WorkItemsEffects] Failed to delete work item:', err);
            return of();
          })
        )
      )
    )
  );

  advanceStage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.advanceWorkItemStage),
      mergeMap(({ id, targetStatus }) =>
        this.http.post<WorkItem>(`${this.apiUrl}/workitems/${id}/advance-stage`, { targetStatus }).pipe(
          map(updated => WorkItemActions.advanceWorkItemStageSuccess({ item: updated })),
          catchError(err => {
            console.error('[WorkItemsEffects] Failed to advance work item stage:', err);
            return of();
          })
        )
      )
    )
  );

  updateQualityGates$ = createEffect(() =>
    this.actions$.pipe(
      ofType(WorkItemActions.updateQualityGates),
      mergeMap(({ id, gates }) =>
        this.http.post<WorkItem>(`${this.apiUrl}/workitems/${id}/quality-gates`, gates).pipe(
          map(updated => WorkItemActions.updateQualityGatesSuccess({ item: updated })),
          catchError(err => {
            console.error('[WorkItemsEffects] Failed to update quality gates:', err);
            return of();
          })
        )
      )
    )
  );
}
