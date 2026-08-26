import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { SprintCapacity, TeamLeave } from '../../models/scrum.models';

export const LeaveActions = createActionGroup({
  source: 'Leaves Feature',
  events: {
    'Load Leaves': emptyProps(),
    'Load Leaves Success': props<{ leaves: TeamLeave[] }>(),
    'Load Leaves Failure': props<{ error: string }>(),

    'Load Capacity': props<{ sprintId: string }>(),
    'Load Capacity Success': props<{ capacity: SprintCapacity | null }>(),
    'Load Capacity Failure': props<{ error: string }>(),

    'Submit Leave': props<{ leave: any }>(),
    'Submit Leave Success': props<{ leave: TeamLeave }>(),

    'Update Leave': props<{ id: string; leave: any }>(),
    'Update Leave Success': props<{ leave: TeamLeave }>(),

    'Delete Leave': props<{ id: string }>(),
    'Delete Leave Success': props<{ id: string }>()
  }
});
