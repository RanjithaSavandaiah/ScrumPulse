import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { ReviewActions } from './reviews.actions';
import { MonthlyFeedback } from '../../models/scrum.models';

@Injectable()
export class ReviewsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadFeedbacks$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ReviewActions.loadFeedbacks),
      switchMap(() =>
        this.http.get<MonthlyFeedback[]>(`${this.apiUrl}/feedback`).pipe(
          map(feedbacks => ReviewActions.loadFeedbacksSuccess({ feedbacks })),
          catchError(err => of(ReviewActions.loadFeedbacksFailure({ error: err.message })))
        )
      )
    )
  );

  submitFeedback$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ReviewActions.submitFeedback),
      mergeMap(({ feedback }) =>
        this.http.post<MonthlyFeedback>(`${this.apiUrl}/feedback`, feedback).pipe(
          map(saved => ReviewActions.submitFeedbackSuccess({ feedback: saved })),
          catchError(err => of(ReviewActions.loadFeedbacksFailure({ error: err.message })))
        )
      )
    )
  );

  updateFeedback$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ReviewActions.updateFeedback),
      mergeMap(({ id, feedback }) =>
        this.http.put<MonthlyFeedback>(`${this.apiUrl}/feedback/${id}`, feedback).pipe(
          map(saved => ReviewActions.updateFeedbackSuccess({ feedback: saved })),
          catchError(err => of(ReviewActions.loadFeedbacksFailure({ error: err.message })))
        )
      )
    )
  );

  deleteFeedback$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ReviewActions.deleteFeedback),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/feedback/${id}`).pipe(
          map(() => ReviewActions.deleteFeedbackSuccess({ id })),
          catchError(err => of(ReviewActions.loadFeedbacksFailure({ error: err.message })))
        )
      )
    )
  );
}
