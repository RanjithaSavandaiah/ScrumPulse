import { createReducer, on } from '@ngrx/store';
import { MonthlyFeedback } from '../../models/scrum.models';
import { ReviewActions } from './reviews.actions';

export interface ReviewsState {
  feedbacks: MonthlyFeedback[];
  loading: boolean;
  error: string | null;
}

export const initialReviewsState: ReviewsState = {
  feedbacks: [],
  loading: false,
  error: null
};

export const reviewsReducer = createReducer(
  initialReviewsState,
  on(ReviewActions.loadFeedbacks, state => ({ ...state, loading: true, error: null })),
  on(ReviewActions.loadFeedbacksSuccess, (state, { feedbacks }) => ({ ...state, feedbacks, loading: false })),
  on(ReviewActions.loadFeedbacksFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(ReviewActions.submitFeedbackSuccess, (state, { feedback }) => ({
    ...state,
    feedbacks: [feedback, ...state.feedbacks]
  })),
  on(ReviewActions.updateFeedbackSuccess, (state, { feedback }) => ({
    ...state,
    feedbacks: state.feedbacks.map(f => (f.id === feedback.id ? feedback : f))
  })),
  on(ReviewActions.deleteFeedbackSuccess, (state, { id }) => ({
    ...state,
    feedbacks: state.feedbacks.filter(f => f.id !== id)
  }))
);
