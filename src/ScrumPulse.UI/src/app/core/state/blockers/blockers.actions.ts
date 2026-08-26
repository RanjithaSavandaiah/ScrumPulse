import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { Blocker } from '../../models/scrum.models';

export const BlockerActions = createActionGroup({
  source: 'Blockers Feature',
  events: {
    'Load Blockers': emptyProps(),
    'Load Blockers Success': props<{ blockers: Blocker[] }>(),
    'Load Blockers Failure': props<{ error: string }>(),

    'Create Blocker': props<{ blocker: any }>(),
    'Create Blocker Success': props<{ blocker: Blocker }>(),

    'Update Blocker': props<{ id: string; blocker: any }>(),
    'Update Blocker Success': props<{ blocker: Blocker }>(),

    'Delete Blocker': props<{ id: string }>(),
    'Delete Blocker Success': props<{ id: string }>(),

    'Resolve Blocker': props<{ id: string; notes?: string }>(),
    'Resolve Blocker Success': props<{ blocker: Blocker }>()
  }
});
