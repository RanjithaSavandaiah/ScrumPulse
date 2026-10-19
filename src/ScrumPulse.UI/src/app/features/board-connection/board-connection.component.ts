import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FieldErrorComponent } from '../../core/components/field-error/field-error.component';
import { IconComponent } from '../../core/components/icon/icon.component';
import { PersistSelectionDirective } from '../../core/directives/persist-selection.directive';
import {
  BoardAssigneeFilter,
  BoardConnection,
  BoardConnectionService,
  BoardStateMapping,
  BoardSyncResult,
  ContentSensitivityPolicy,
  SaveBoardConnectionRequest,
  WorkItemStatus
} from '../../core/services/board-connection.service';

interface StateMappingRow {
  boardState: string;
  status: WorkItemStatus;
}

/** Tracked so only the button pressed shows a spinner. */
type PendingAction = 'save' | 'test' | 'sync' | 'resync' | 'import' | 'delete' | null;

/**
 * Duplicates the server's HTTPS check to explain the mistake while the user is still looking
 * at the field. The server stays the authority and also restricts the host.
 */
function httpsUrl(control: AbstractControl): ValidationErrors | null {
  const value = (control.value as string | null)?.trim();

  if (!value) {
    return null;
  }

  let parsed: URL;

  try {
    parsed = new URL(value);
  } catch {
    return { message: 'Enter a full URL, for example https://dev.azure.com/contoso.' };
  }

  return parsed.protocol === 'https:'
    ? null
    : { message: 'The URL must start with https:// so the token is never sent in clear text.' };
}

/**
 * Configures the squad's Azure DevOps board link and runs imports against it: a token for
 * automatic sync, a CSV export for squads who will never be issued one, and a manual state
 * mapping for bespoke process templates.
 *
 * The credential is write-only. There is no stored value to render, so the token field stays
 * blank on edit, and blank is how the user says "keep the one you have".
 */
@Component({
  selector: 'app-board-connection',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    IconComponent,
    FieldErrorComponent,
    PersistSelectionDirective
  ],
  templateUrl: './board-connection.component.html',
  styleUrl: './board-connection.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BoardConnectionComponent {
  private readonly boardConnections = inject(BoardConnectionService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  /** Server limits, mirrored to catch over-length values before the round trip. */
  private static readonly MaxOrganizationUrlLength = 300;
  private static readonly MaxProjectNameLength = 200;
  private static readonly MaxAreaPathLength = 400;
  private static readonly MinTokenLength = 8;
  private static readonly MaxTokenLength = 500;

  protected readonly form = this.formBuilder.nonNullable.group({
    organizationUrl: [
      '',
      [
        Validators.required,
        Validators.maxLength(BoardConnectionComponent.MaxOrganizationUrlLength),
        httpsUrl
      ]
    ],
    projectName: [
      '',
      [Validators.required, Validators.maxLength(BoardConnectionComponent.MaxProjectNameLength)]
    ],
    teamOrAreaPath: ['', [Validators.maxLength(BoardConnectionComponent.MaxAreaPathLength)]],
    personalAccessToken: [
      '',
      [
        Validators.minLength(BoardConnectionComponent.MinTokenLength),
        Validators.maxLength(BoardConnectionComponent.MaxTokenLength)
      ]
    ],
    assigneeFilter: ['RosterMembersOnly'],
    contentPolicy: ['MetricsOnly'],
    isEnabled: [true]
  });

  protected readonly connection = signal<BoardConnection | null>(null);
  protected readonly stateMappingRows = signal<StateMappingRow[]>([]);
  protected readonly syncResult = signal<BoardSyncResult | null>(null);
  protected readonly testResult = signal<{ succeeded: boolean; message: string } | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly statusMessage = signal<string | null>(null);
  protected readonly pendingAction = signal<PendingAction>(null);
  protected readonly isLoading = signal(true);
  protected readonly isConfirmingDelete = signal(false);

  /** Reveals every error after a rejected submit, rather than one per field touched. */
  protected readonly hasAttemptedSubmit = signal(false);

  protected readonly isConnected = computed(() => this.connection()?.hasStoredCredential === true);

  /** Configured but switched off, which reads differently to never connected. */
  protected readonly isSyncingPaused = computed(() => this.connection()?.isEnabled === false);

  protected readonly assigneeFilters = [
    {
      value: 'RosterMembersOnly',
      label: 'Roster members only',
      hint: 'Imports only work assigned to someone on this squad.'
    },
    {
      value: 'EveryoneInAreaPath',
      label: 'Everyone in the area path',
      hint: 'Only safe where the area path already isolates this squad.'
    }
  ] as const;

  protected readonly contentPolicies = [
    {
      value: 'MetricsOnly',
      label: 'Metrics only (recommended)',
      hint: 'States, dates, points and assignee. No titles, no descriptions.'
    },
    {
      value: 'RedactedTitles',
      label: 'Redacted titles',
      hint: 'Titles with URLs and anything resembling a secret stripped out.'
    },
    {
      value: 'RedactedTitlesAndDescriptions',
      label: 'Redacted titles and descriptions',
      hint: 'Private deployments only. Descriptions are where pasted credentials turn up.'
    }
  ] as const;

  protected readonly workItemStatuses = [
    { value: 'Backlog', label: 'Backlog' },
    { value: 'InProgress', label: 'In progress' },
    { value: 'PrCreated', label: 'PR created' },
    { value: 'PrApproved', label: 'PR approved' },
    { value: 'Merged', label: 'Merged' },
    { value: 'InQa', label: 'In QA' },
    { value: 'Done', label: 'Done' }
  ] as const;

  /** Backs the hint that follows the dropdown instead of listing every option beneath it. */
  private readonly assigneeFilterValue = toSignal(this.form.controls.assigneeFilter.valueChanges, {
    initialValue: this.form.controls.assigneeFilter.value
  });

  private readonly contentPolicyValue = toSignal(this.form.controls.contentPolicy.valueChanges, {
    initialValue: this.form.controls.contentPolicy.value
  });

  protected readonly selectedAssigneeFilterHint = computed(
    () =>
      this.assigneeFilters.find((option) => option.value === this.assigneeFilterValue())
        ?.hint ?? ''
  );

  protected readonly selectedContentPolicyHint = computed(
    () =>
      this.contentPolicies.find((option) => option.value === this.contentPolicyValue())
        ?.hint ?? ''
  );

  constructor() {
    this.loadConnection();
  }

  protected save(): void {
    this.hasAttemptedSubmit.set(true);
    this.clearOutcomes();

    if (this.form.invalid) {
      // Each field carries its own message; the banner alone would not say which.
      this.form.markAllAsTouched();
      this.errorMessage.set('Fix the highlighted fields, then save again.');
      return;
    }

    if (!this.isConnected() && !this.form.controls.personalAccessToken.value.trim()) {
      // Legitimate on edit, meaningless on a first save: the failure would surface later as
      // an unexplained 401 from Azure DevOps.
      this.form.controls.personalAccessToken.markAsTouched();
      this.errorMessage.set(
        'A personal access token is needed the first time, so the sync has something to authenticate with.'
      );
      return;
    }

    this.pendingAction.set('save');

    this.boardConnections
      .saveConnection(this.buildSaveRequest())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (connection) => {
          this.applyConnection(connection);
          this.pendingAction.set(null);
          this.statusMessage.set('Connection saved.');
        },
        error: (error: unknown) => this.reportFailure(error, 'The connection could not be saved.')
      });
  }

  protected testConnection(): void {
    this.clearOutcomes();
    this.pendingAction.set('test');

    this.boardConnections
      .testConnection()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.pendingAction.set(null);
          this.testResult.set({
            succeeded: result.succeeded,
            message:
              result.message ??
              (result.succeeded
                ? 'Azure DevOps accepted the stored token.'
                : 'Azure DevOps rejected the stored token.')
          });
        },
        error: (error: unknown) => this.reportFailure(error, 'The connection could not be tested.')
      });
  }

  protected sync(fullResync: boolean): void {
    this.clearOutcomes();
    this.pendingAction.set(fullResync ? 'resync' : 'sync');

    this.boardConnections
      .sync(fullResync)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.pendingAction.set(null);
          this.syncResult.set(result);
          this.loadConnection();
        },
        error: (error: unknown) => this.reportFailure(error, 'The sync did not complete.')
      });
  }

  protected importFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    this.clearOutcomes();
    this.pendingAction.set('import');

    this.boardConnections
      .importFile(file)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.pendingAction.set(null);
          this.syncResult.set(result);
          this.loadConnection();
        },
        error: (error: unknown) => this.reportFailure(error, 'The file could not be imported.')
      });

    // Otherwise re-picking the same corrected file raises no change event.
    input.value = '';
  }

  protected requestDelete(): void {
    this.clearOutcomes();
    this.isConfirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.isConfirmingDelete.set(false);
  }

  protected confirmDelete(): void {
    this.isConfirmingDelete.set(false);
    this.pendingAction.set('delete');

    this.boardConnections
      .deleteConnection()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pendingAction.set(null);
          this.connection.set(null);
          this.form.reset({
            organizationUrl: '',
            projectName: '',
            teamOrAreaPath: '',
            personalAccessToken: '',
            assigneeFilter: 'RosterMembersOnly',
            contentPolicy: 'MetricsOnly',
            isEnabled: true
          });
          this.hasAttemptedSubmit.set(false);
          this.loadStateMappingDefaults();
          this.statusMessage.set('Connection removed and the stored token destroyed.');
        },
        error: (error: unknown) => this.reportFailure(error, 'The connection could not be removed.')
      });
  }

  protected onStateMapped(boardState: string, event: Event): void {
    const status = (event.target as HTMLSelectElement).value as WorkItemStatus;

    this.stateMappingRows.update((rows) =>
      rows.map((row) => (row.boardState === boardState ? { ...row, status } : row))
    );
  }

  /** The token field is blank on edit, which is how the user keeps the stored one. */
  protected get tokenPlaceholder(): string {
    return this.isConnected()
      ? 'Leave blank to keep the stored token'
      : 'Paste the personal access token';
  }

  private loadConnection(): void {
    this.boardConnections
      .getConnection()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (connection) => {
          this.isLoading.set(false);

          if (connection) {
            this.applyConnection(connection);
            return;
          }

          this.loadStateMappingDefaults();
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          this.reportFailure(error, 'The board connection could not be loaded.');
        }
      });
  }

  private loadStateMappingDefaults(): void {
    this.boardConnections
      .getStateMappingDefaults()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (defaults) => this.setStateMappingRows(defaults.azureDevOpsDefaults),

        // The form still works without the grid, and the server applies the same defaults
        // for an empty mapping, so an error here would wrongly suggest the save failed.
        error: () => this.stateMappingRows.set([])
      });
  }

  private applyConnection(connection: BoardConnection): void {
    this.connection.set(connection);

    this.form.patchValue({
      organizationUrl: connection.organizationUrl,
      projectName: connection.projectName,
      teamOrAreaPath: connection.teamOrAreaPath ?? '',
      assigneeFilter: connection.assigneeFilter,
      contentPolicy: connection.contentPolicy,
      isEnabled: connection.isEnabled
    });

    // Never patched. A placeholder or row of dots is a value the user could submit by
    // accident, overwriting a working credential with decoration.
    this.form.controls.personalAccessToken.reset('');

    const mapping = connection.stateMapping ?? {};

    if (Object.keys(mapping).length > 0) {
      this.setStateMappingRows(mapping);
      return;
    }

    this.loadStateMappingDefaults();
  }

  private setStateMappingRows(mapping: BoardStateMapping): void {
    this.stateMappingRows.set(
      Object.entries(mapping).map(([boardState, status]) => ({ boardState, status }))
    );
  }

  private buildSaveRequest(): SaveBoardConnectionRequest {
    const value = this.form.getRawValue();
    const token = value.personalAccessToken.trim();
    const areaPath = value.teamOrAreaPath.trim();

    const request: SaveBoardConnectionRequest = {
      organizationUrl: value.organizationUrl.trim(),
      projectName: value.projectName.trim(),
      teamOrAreaPath: areaPath.length > 0 ? areaPath : null,
      // No numeric conversion: the API speaks enum names and a select already yields one.
      assigneeFilter: value.assigneeFilter as BoardAssigneeFilter,
      contentPolicy: value.contentPolicy as ContentSensitivityPolicy,
      isEnabled: value.isEnabled,
      stateMapping: this.buildStateMapping()
    };

    // Absent rather than null. Both mean "keep the stored one", but omitting it keeps the
    // field out of proxy logs entirely.
    return token.length > 0 ? { ...request, personalAccessToken: token } : request;
  }

  private buildStateMapping(): BoardStateMapping {
    return this.stateMappingRows().reduce<BoardStateMapping>((mapping, row) => {
      mapping[row.boardState] = row.status;
      return mapping;
    }, {});
  }

  private clearOutcomes(): void {
    this.errorMessage.set(null);
    this.statusMessage.set(null);
    this.testResult.set(null);
    this.syncResult.set(null);
  }

  /**
   * Prefers the server's own explanation. A rejected token or a CSV from the wrong query is
   * actionable; a generic apology is not.
   */
  private reportFailure(error: unknown, fallback: string): void {
    this.pendingAction.set(null);

    const detail = (error as { error?: { detail?: string; title?: string } } | null)?.error;
    const serverMessage = detail?.detail ?? detail?.title;

    this.errorMessage.set(serverMessage?.trim() || fallback);
  }
}
