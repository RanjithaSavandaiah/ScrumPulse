import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { TechDebtItem, TechTalkLog } from '../../models/scrum.models';

export const TechHubActions = createActionGroup({
  source: 'Tech Hub Feature',
  events: {
    'Load Tech Hub': emptyProps(),
    'Load Tech Hub Success': props<{ techDebt: TechDebtItem[]; techTalks: TechTalkLog[] }>(),
    'Load Tech Hub Failure': props<{ error: string }>(),

    'Create Tech Debt': props<{ item: any }>(),
    'Create Tech Debt Success': props<{ item: TechDebtItem }>(),

    'Update Tech Debt': props<{ id: string; item: any }>(),
    'Update Tech Debt Success': props<{ item: TechDebtItem }>(),

    'Delete Tech Debt': props<{ id: string }>(),
    'Delete Tech Debt Success': props<{ id: string }>(),

    'Resolve Tech Debt': props<{ id: string; status: string }>(),
    'Resolve Tech Debt Success': props<{ item: TechDebtItem }>(),

    'Log Tech Talk': props<{ log: any }>(),
    'Log Tech Talk Success': props<{ log: TechTalkLog }>(),

    'Update Tech Talk': props<{ id: string; log: any }>(),
    'Update Tech Talk Success': props<{ log: TechTalkLog }>(),

    'Delete Tech Talk': props<{ id: string }>(),
    'Delete Tech Talk Success': props<{ id: string }>()
  }
});
