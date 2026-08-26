import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { DeveloperPrMetrics, PullRequestLog } from '../../models/scrum.models';

export const PullRequestActions = createActionGroup({
  source: 'Pull Requests Feature',
  events: {
    'Load Pull Requests': props<{ sprintId?: string }>(),
    'Load Pull Requests Success': props<{ prLogs: PullRequestLog[] }>(),
    'Load Pull Requests Failure': props<{ error: string }>(),

    'Load Developer Pr Metrics': props<{ sprintId?: string }>(),
    'Load Developer Pr Metrics Success': props<{ metrics: DeveloperPrMetrics[] }>(),
    'Load Developer Pr Metrics Failure': props<{ error: string }>(),

    'Create Pull Request Log': props<{ request: any }>(),
    'Create Pull Request Log Success': props<{ log: PullRequestLog }>(),

    'Delete Pull Request Log': props<{ id: string }>(),
    'Delete Pull Request Log Success': props<{ id: string }>()
  }
});
