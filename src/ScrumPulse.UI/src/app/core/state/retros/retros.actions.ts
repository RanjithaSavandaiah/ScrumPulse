import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { RetroActionItem, RetroCard } from '../../models/scrum.models';

export const RetroActions = createActionGroup({
  source: 'Retros Feature',
  events: {
    'Load Retros': props<{ sprintId?: string }>(),
    'Load Retros Success': props<{ cards: RetroCard[]; actions: RetroActionItem[] }>(),
    'Load Retros Failure': props<{ error: string }>(),

    'Create Retro Card': props<{ card: any }>(),
    'Create Retro Card Success': props<{ card: RetroCard }>(),

    'Update Retro Card': props<{ id: string; card: any }>(),
    'Update Retro Card Success': props<{ card: RetroCard }>(),

    'Delete Retro Card': props<{ id: string }>(),
    'Delete Retro Card Success': props<{ id: string }>(),

    'Vote Retro Card': props<{ id: string }>(),
    'Vote Retro Card Success': props<{ card: RetroCard }>(),

    'Create Retro Action': props<{ action: any }>(),
    'Create Retro Action Success': props<{ action: RetroActionItem }>(),

    'Update Retro Action': props<{ id: string; action: any }>(),
    'Update Retro Action Success': props<{ action: RetroActionItem }>(),

    'Delete Retro Action': props<{ id: string }>(),
    'Delete Retro Action Success': props<{ id: string }>(),

    'Toggle Retro Action': props<{ id: string }>(),
    'Toggle Retro Action Success': props<{ action: RetroActionItem }>()
  }
});
