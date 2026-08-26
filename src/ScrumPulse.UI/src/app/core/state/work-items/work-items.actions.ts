import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { WorkItem } from '../../models/scrum.models';

export const WorkItemActions = createActionGroup({
  source: 'Work Items Feature',
  events: {
    'Load Work Items': props<{ sprintId?: string }>(),
    'Load Work Items Success': props<{ items: WorkItem[] }>(),
    'Load Work Items Failure': props<{ error: string }>(),

    'Create Work Item': props<{ item: any }>(),
    'Create Work Item Success': props<{ item: WorkItem }>(),

    'Update Work Item': props<{ id: string; item: any }>(),
    'Update Work Item Success': props<{ item: WorkItem }>(),

    'Delete Work Item': props<{ id: string }>(),
    'Delete Work Item Success': props<{ id: string }>(),

    'Advance Work Item Stage': props<{ id: string; targetStatus: string }>(),
    'Advance Work Item Stage Success': props<{ item: WorkItem }>(),

    'Update Quality Gates': props<{ id: string; gates: any }>(),
    'Update Quality Gates Success': props<{ item: WorkItem }>()
  }
});
