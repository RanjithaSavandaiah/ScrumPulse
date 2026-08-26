import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { MonthlyFeedback } from '../../models/scrum.models';

export const ReviewActions = createActionGroup({
  source: 'Reviews Feature',
  events: {
    'Load Feedbacks': emptyProps(),
    'Load Feedbacks Success': props<{ feedbacks: MonthlyFeedback[] }>(),
    'Load Feedbacks Failure': props<{ error: string }>(),

    'Submit Feedback': props<{ feedback: any }>(),
    'Submit Feedback Success': props<{ feedback: MonthlyFeedback }>(),

    'Update Feedback': props<{ id: string; feedback: any }>(),
    'Update Feedback Success': props<{ feedback: MonthlyFeedback }>(),

    'Delete Feedback': props<{ id: string }>(),
    'Delete Feedback Success': props<{ id: string }>()
  }
});
