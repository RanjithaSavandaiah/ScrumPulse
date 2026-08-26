import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { Sprint } from '../../models/scrum.models';

export const SprintActions = createActionGroup({
  source: 'Sprints Feature',
  events: {
    'Load Sprints': emptyProps(),
    'Load Sprints Success': props<{ sprints: Sprint[] }>(),
    'Load Sprints Failure': props<{ error: string }>(),

    'Create Sprint': props<{ sprint: Partial<Sprint> }>(),
    'Create Sprint Success': props<{ sprint: Sprint }>(),

    'Update Sprint': props<{ id: string; sprint: Partial<Sprint> }>(),
    'Update Sprint Success': props<{ sprint: Sprint }>(),

    'Activate Sprint': props<{ sprintId: string }>(),
    'Activate Sprint Success': props<{ sprint: Sprint }>(),

    'Delete Sprint': props<{ sprintId: string }>(),
    'Delete Sprint Success': props<{ sprintId: string }>()
  }
});
