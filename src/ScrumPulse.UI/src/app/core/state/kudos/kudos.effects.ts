import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { KudosActions } from './kudos.actions';
import { KudosCard } from '../../models/scrum.models';

@Injectable()
export class KudosEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadKudos$ = createEffect(() =>
    this.actions$.pipe(
      ofType(KudosActions.loadKudos),
      switchMap(() =>
        this.http.get<KudosCard[]>(`${this.apiUrl}/kudos`).pipe(
          map(kudos => KudosActions.loadKudosSuccess({ kudos })),
          catchError(err => of(KudosActions.loadKudosFailure({ error: err.message })))
        )
      )
    )
  );

  giveKudos$ = createEffect(() =>
    this.actions$.pipe(
      ofType(KudosActions.giveKudos),
      mergeMap(({ kudos }) =>
        this.http.post<KudosCard>(`${this.apiUrl}/kudos`, kudos).pipe(
          map(saved => KudosActions.giveKudosSuccess({ kudos: saved })),
          catchError(() => of())
        )
      )
    )
  );

  addKudosReaction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(KudosActions.addKudosReaction),
      mergeMap(({ id, reactionKey }) =>
        this.http.post<KudosCard>(`${this.apiUrl}/kudos/${id}/react`, { reactionType: reactionKey }).pipe(
          map(updated => KudosActions.addKudosReactionSuccess({ kudos: updated })),
          catchError(() => of())
        )
      )
    )
  );
}
