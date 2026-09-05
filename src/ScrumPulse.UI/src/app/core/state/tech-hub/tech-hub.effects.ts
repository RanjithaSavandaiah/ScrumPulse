import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { forkJoin, of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { TechHubActions } from './tech-hub.actions';
import { TechDebtItem, TechTalkLog } from '../../models/scrum.models';

@Injectable()
export class TechHubEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadTechHub$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.loadTechHub),
      switchMap(() =>
        forkJoin({
          techDebt: this.http.get<TechDebtItem[]>(`${this.apiUrl}/techhub/tech-debt`).pipe(
            catchError(err => {
              console.error('[TechHubEffects] Failed to load tech debt items:', err);
              return of([]);
            })
          ),
          techTalks: this.http.get<TechTalkLog[]>(`${this.apiUrl}/techhub/tech-talks`).pipe(
            catchError(err => {
              console.error('[TechHubEffects] Failed to load tech talks:', err);
              return of([]);
            })
          )
        }).pipe(
          map(res => TechHubActions.loadTechHubSuccess({ techDebt: res.techDebt, techTalks: res.techTalks })),
          catchError(err => of(TechHubActions.loadTechHubFailure({ error: err.message })))
        )
      )
    )
  );

  createTechDebt$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.createTechDebt),
      mergeMap(({ item }) =>
        this.http.post<TechDebtItem>(`${this.apiUrl}/techhub/tech-debt`, item).pipe(
          map(saved => TechHubActions.createTechDebtSuccess({ item: saved })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to create tech debt:', err);
            return of();
          })
        )
      )
    )
  );

  updateTechDebt$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.updateTechDebt),
      mergeMap(({ id, item }) =>
        this.http.put<TechDebtItem>(`${this.apiUrl}/techhub/tech-debt/${id}`, item).pipe(
          map(saved => TechHubActions.updateTechDebtSuccess({ item: saved })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to update tech debt:', err);
            return of();
          })
        )
      )
    )
  );

  deleteTechDebt$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.deleteTechDebt),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/techhub/tech-debt/${id}`).pipe(
          map(() => TechHubActions.deleteTechDebtSuccess({ id })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to delete tech debt:', err);
            return of();
          })
        )
      )
    )
  );

  resolveTechDebt$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.resolveTechDebt),
      mergeMap(({ id, status }) =>
        this.http.post<TechDebtItem>(`${this.apiUrl}/techhub/tech-debt/${id}/resolve`, { status }).pipe(
          map(saved => TechHubActions.resolveTechDebtSuccess({ item: saved })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to resolve tech debt:', err);
            return of();
          })
        )
      )
    )
  );

  logTechTalk$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.logTechTalk),
      mergeMap(({ log }) =>
        this.http.post<TechTalkLog>(`${this.apiUrl}/techhub/tech-talks`, log).pipe(
          map(saved => TechHubActions.logTechTalkSuccess({ log: saved })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to log tech talk:', err);
            return of();
          })
        )
      )
    )
  );

  updateTechTalk$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.updateTechTalk),
      mergeMap(({ id, log }) =>
        this.http.put<TechTalkLog>(`${this.apiUrl}/techhub/tech-talks/${id}`, log).pipe(
          map(saved => TechHubActions.updateTechTalkSuccess({ log: saved })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to update tech talk:', err);
            return of();
          })
        )
      )
    )
  );

  deleteTechTalk$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TechHubActions.deleteTechTalk),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/techhub/tech-talks/${id}`).pipe(
          map(() => TechHubActions.deleteTechTalkSuccess({ id })),
          catchError(err => {
            console.error('[TechHubEffects] Failed to delete tech talk:', err);
            return of();
          })
        )
      )
    )
  );
}
