import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { TeamMemberActions } from './team-members.actions';
import { TeamMember } from '../../models/scrum.models';

@Injectable()
export class TeamMembersEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadTeamMembers$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TeamMemberActions.loadTeamMembers),
      switchMap(() =>
        this.http.get<TeamMember[]>(`${this.apiUrl}/teammembers?all=true`).pipe(
          map(members => TeamMemberActions.loadTeamMembersSuccess({ members })),
          catchError(err => of(TeamMemberActions.loadTeamMembersFailure({ error: err.message })))
        )
      )
    )
  );

  createMember$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TeamMemberActions.createTeamMember),
      mergeMap(({ member }) =>
        this.http.post<TeamMember>(`${this.apiUrl}/teammembers`, member).pipe(
          map(created => TeamMemberActions.createTeamMemberSuccess({ member: created })),
          catchError(err => {
            console.error('[TeamMembersEffects] Failed to create team member:', err);
            return of();
          })
        )
      )
    )
  );

  deleteMember$ = createEffect(() =>
    this.actions$.pipe(
      ofType(TeamMemberActions.deleteTeamMember),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/teammembers/${id}`).pipe(
          map(() => TeamMemberActions.deleteTeamMemberSuccess({ id })),
          catchError(err => {
            console.error('[TeamMembersEffects] Failed to delete team member:', err);
            return of();
          })
        )
      )
    )
  );
}
