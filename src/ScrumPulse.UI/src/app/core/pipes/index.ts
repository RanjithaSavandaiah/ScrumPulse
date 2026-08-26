import { CleanNamePipe } from './clean-name.pipe';
import { RoleLabelPipe } from './role-label.pipe';
import { InitialsPipe } from './initials.pipe';
import { BadgeLabelPipe } from './badge-label.pipe';

export * from './clean-name.pipe';
export * from './role-label.pipe';
export * from './initials.pipe';
export * from './badge-label.pipe';

export const CORE_PIPES = [
  CleanNamePipe,
  RoleLabelPipe,
  InitialsPipe,
  BadgeLabelPipe
] as const;
