import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ReviewsState } from './reviews.reducer';

export const selectReviewsState = createFeatureSelector<ReviewsState>('reviews');

export const selectMonthlyFeedbacks = createSelector(selectReviewsState, state => state.feedbacks);
export const selectReviewsLoading = createSelector(selectReviewsState, state => state.loading);
