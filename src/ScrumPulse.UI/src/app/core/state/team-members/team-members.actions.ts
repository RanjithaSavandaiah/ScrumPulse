import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { RoleType, TeamMember } from '../../models/scrum.models';

export const TeamMemberActions = createActionGroup({
  source: 'Team Members Feature',
  events: {
    'Load Team Members': emptyProps(),
    'Load Team Members Success': props<{ members: TeamMember[] }>(),
    'Load Team Members Failure': props<{ error: string }>(),

    'Create Team Member': props<{ member: Partial<TeamMember> }>(),
    'Create Team Member Success': props<{ member: TeamMember }>(),

    'Delete Team Member': props<{ id: string }>(),
    'Delete Team Member Success': props<{ id: string }>(),

    'Set Current Role': props<{ role: RoleType }>(),
    'Toggle Dark Mode': emptyProps()
  }
});
