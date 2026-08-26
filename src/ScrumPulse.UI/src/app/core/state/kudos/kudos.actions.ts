import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { KudosCard } from '../../models/scrum.models';

export const KudosActions = createActionGroup({
  source: 'Kudos Feature',
  events: {
    'Load Kudos': emptyProps(),
    'Load Kudos Success': props<{ kudos: KudosCard[] }>(),
    'Load Kudos Failure': props<{ error: string }>(),

    'Give Kudos': props<{ kudos: any }>(),
    'Give Kudos Success': props<{ kudos: KudosCard }>(),

    'Add Kudos Reaction': props<{ id: string; reactionKey: string }>(),
    'Add Kudos Reaction Success': props<{ kudos: KudosCard }>()
  }
});
