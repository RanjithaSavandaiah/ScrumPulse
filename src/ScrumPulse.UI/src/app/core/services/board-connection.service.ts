import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { WorkItemStatus } from '../models/scrum.models';

// Re-exported so the board screen takes its whole contract from one import.
export type { WorkItemStatus };

// String unions, not numeric enums: the API registers a JsonStringEnumConverter and speaks
// enum names on the wire.
export type BoardAssigneeFilter = 'RosterMembersOnly' | 'EveryoneInAreaPath';

export type ContentSensitivityPolicy =
  | 'MetricsOnly'
  | 'RedactedTitles'
  | 'RedactedTitlesAndDescriptions';

export type WorkItemOrigin = 'Manual' | 'AzureDevOps' | 'FileImport';

/** Azure DevOps state name to ScrumPulse stage. */
export type BoardStateMapping = Record<string, WorkItemStatus>;

/** No token field, and never one; `hasStoredCredential` is all the API says about it. */
export interface BoardConnection {
  id: string;
  provider: WorkItemOrigin;
  organizationUrl: string;
  projectName: string;
  teamOrAreaPath: string | null;
  hasStoredCredential: boolean;
  assigneeFilter: BoardAssigneeFilter;
  contentPolicy: ContentSensitivityPolicy;
  isEnabled: boolean;
  lastSyncedAtUtc: string | null;
  lastSyncStatus: string | null;
  lastSyncItemCount: number;
  stateMapping: BoardStateMapping;
}

/** Omitting `personalAccessToken` tells the server to keep the stored one. */
export interface SaveBoardConnectionRequest {
  organizationUrl: string;
  projectName: string;
  teamOrAreaPath?: string | null;
  personalAccessToken?: string;
  assigneeFilter: BoardAssigneeFilter;
  contentPolicy: ContentSensitivityPolicy;
  isEnabled: boolean;
  stateMapping?: BoardStateMapping;
}

export interface BoardSyncResult {
  importedCount: number;
  createdCount: number;
  unmappedStates: string[];
  unmatchedAssignees: string[];
  warnings: string[];
  syncedAtUtc: string;
}

export interface BoardConnectionTestResult {
  succeeded: boolean;
  message: string | null;
}

/** Stock Azure DevOps state names, so the mapping grid is never blank. */
export interface BoardStateMappingDefaults {
  azureDevOpsDefaults: BoardStateMapping;
}

const StatusToCode: Record<string, number> = {
  Backlog: 0,
  InProgress: 1,
  PrCreated: 2,
  PrApproved: 3,
  Merged: 4,
  InQa: 5,
  Done: 6
};

const CodeToStatus: Record<number, WorkItemStatus> = {
  0: 'Backlog',
  1: 'InProgress',
  2: 'PrCreated',
  3: 'PrApproved',
  4: 'Merged',
  5: 'InQa',
  6: 'Done'
};

/** Talks to /api/board-connections, every endpoint of which is Scrum Master only server side. */
@Injectable({ providedIn: 'root' })
export class BoardConnectionService {
  private static readonly BaseUrl = '/api/board-connections';

  private readonly http = inject(HttpClient);

  /** Unconfigured answers 204, so the status is checked rather than trusting a falsy body. */
  getConnection(): Observable<BoardConnection | null> {
    return this.http
      .get<BoardConnection>(BoardConnectionService.BaseUrl, { observe: 'response' })
      .pipe(map((response) => (response.status === 204 ? null : response.body)));
  }

  saveConnection(request: SaveBoardConnectionRequest): Observable<BoardConnection> {
    const payload = {
      ...request,
      stateMapping: request.stateMapping
        ? Object.fromEntries(
            Object.entries(request.stateMapping).map(([k, v]) => [
              k,
              typeof v === 'string' && v in StatusToCode ? StatusToCode[v] : v
            ])
          )
        : undefined
    };
    return this.http.put<BoardConnection>(BoardConnectionService.BaseUrl, payload);
  }

  deleteConnection(): Observable<void> {
    return this.http.delete<void>(BoardConnectionService.BaseUrl);
  }

  testConnection(): Observable<BoardConnectionTestResult> {
    return this.http.post<BoardConnectionTestResult>(`${BoardConnectionService.BaseUrl}/test`, {});
  }

  /**
   * @param fullResync Re-reads the whole backlog instead of only what changed. Worth it after
   * editing the state mapping, wasteful otherwise.
   */
  sync(fullResync = false): Observable<BoardSyncResult> {
    return this.http.post<BoardSyncResult>(
      `${BoardConnectionService.BaseUrl}/sync`,
      {},
      { params: { fullResync } }
    );
  }

  /**
   * The field name must be "file" to bind to the controller's IFormFile parameter; anything
   * else arrives null and the request is rejected as empty.
   */
  importFile(file: File): Observable<BoardSyncResult> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.http.post<BoardSyncResult>(`${BoardConnectionService.BaseUrl}/import`, body);
  }

  getStateMappingDefaults(): Observable<BoardStateMappingDefaults> {
    return this.http
      .get<{ azureDevOpsDefaults: Record<string, number | WorkItemStatus> }>(
        `${BoardConnectionService.BaseUrl}/state-mapping-defaults`
      )
      .pipe(
        map((response) => ({
          azureDevOpsDefaults: Object.fromEntries(
            Object.entries(response.azureDevOpsDefaults || {}).map(([k, v]) => [
              k,
              typeof v === 'number' && v in CodeToStatus ? CodeToStatus[v] : v
            ])
          ) as BoardStateMapping
        }))
      );
  }
}
