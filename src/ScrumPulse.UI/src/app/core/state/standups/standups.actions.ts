import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { DailyStandup } from '../../models/scrum.models';

export const StandupActions = createActionGroup({
  source: 'Standups Feature',
  events: {
    'Load Standups': emptyProps(),
    'Load Standups Success': props<{ standups: DailyStandup[] }>(),
    'Load Standups Failure': props<{ error: string }>(),

    'Submit Standup': props<{ standup: any }>(),
    'Submit Standup Success': props<{ standup: DailyStandup }>(),

    'Update Standup': props<{ id: string; standup: any }>(),
    'Update Standup Success': props<{ standup: DailyStandup }>(),

    'Delete Standup': props<{ id: string }>(),
    'Delete Standup Success': props<{ id: string }>(),

    'Clear All Standups': emptyProps(),
    'Clear All Standups Success': emptyProps()
  }
});
