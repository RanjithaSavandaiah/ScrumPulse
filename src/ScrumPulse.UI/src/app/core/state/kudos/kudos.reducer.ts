import { createReducer, on } from '@ngrx/store';
import { KudosCard } from '../../models/scrum.models';
import { KudosActions } from './kudos.actions';

export interface KudosState {
  kudos: KudosCard[];
  loading: boolean;
  error: string | null;
}

export const initialKudosState: KudosState = {
  kudos: [],
  loading: false,
  error: null
};

export const kudosReducer = createReducer(
  initialKudosState,
  on(KudosActions.loadKudos, state => ({ ...state, loading: true, error: null })),
  on(KudosActions.loadKudosSuccess, (state, { kudos }) => ({ ...state, kudos, loading: false })),
  on(KudosActions.loadKudosFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(KudosActions.giveKudosSuccess, (state, { kudos }) => ({
    ...state,
    kudos: [kudos, ...state.kudos]
  })),
  on(KudosActions.addKudosReaction, (state, { id, reactionKey }) => ({
    ...state,
    kudos: state.kudos.map(k => {
      if (k.id !== id) return k;
      const reactions = { ...(k.reactionEmojis || {}) };
      reactions[reactionKey] = (reactions[reactionKey] || 0) + 1;
      return { ...k, reactionEmojis: reactions };
    })
  })),
  on(KudosActions.addKudosReactionSuccess, (state, { kudos }) => ({
    ...state,
    kudos: state.kudos.map(k => (k.id === kudos.id ? kudos : k))
  }))
);
