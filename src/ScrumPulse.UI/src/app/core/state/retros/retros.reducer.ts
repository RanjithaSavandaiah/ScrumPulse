import { createReducer, on } from '@ngrx/store';
import { RetroActionItem, RetroCard } from '../../models/scrum.models';
import { RetroActions } from './retros.actions';

export interface RetrosState {
  cards: RetroCard[];
  actions: RetroActionItem[];
  loading: boolean;
  error: string | null;
}

export const initialRetrosState: RetrosState = {
  cards: [],
  actions: [],
  loading: false,
  error: null
};

export const retrosReducer = createReducer(
  initialRetrosState,
  on(RetroActions.loadRetros, state => ({ ...state, loading: true, error: null })),
  on(RetroActions.loadRetrosSuccess, (state, { cards, actions }) => ({ ...state, cards, actions, loading: false })),
  on(RetroActions.loadRetrosFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(RetroActions.createRetroCardSuccess, (state, { card }) => ({
    ...state,
    cards: [card, ...state.cards]
  })),
  on(RetroActions.updateRetroCardSuccess, (state, { card }) => ({
    ...state,
    cards: state.cards.map(c => (c.id === card.id ? card : c))
  })),
  on(RetroActions.deleteRetroCardSuccess, (state, { id }) => ({
    ...state,
    cards: state.cards.filter(c => c.id !== id)
  })),
  on(RetroActions.voteRetroCard, (state, { id }) => ({
    ...state,
    cards: state.cards.map(c => (c.id === id ? { ...c, upvotesCount: (c.upvotesCount || 0) + 1 } : c))
  })),
  on(RetroActions.voteRetroCardSuccess, (state, { card }) => ({
    ...state,
    cards: state.cards.map(c => (c.id === card.id ? card : c))
  })),
  on(RetroActions.createRetroActionSuccess, (state, { action }) => ({
    ...state,
    actions: [action, ...state.actions]
  })),
  on(RetroActions.updateRetroActionSuccess, (state, { action }) => ({
    ...state,
    actions: state.actions.map(a => (a.id === action.id ? action : a))
  })),
  on(RetroActions.deleteRetroActionSuccess, (state, { id }) => ({
    ...state,
    actions: state.actions.filter(a => a.id !== id)
  })),
  on(RetroActions.toggleRetroActionSuccess, (state, { action }) => ({
    ...state,
    actions: state.actions.map(a => (a.id === action.id ? action : a))
  }))
);
