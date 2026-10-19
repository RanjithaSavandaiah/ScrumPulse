import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  BoardAssigneeFilter,
  BoardConnection,
  BoardConnectionService,
  ContentSensitivityPolicy,
  SaveBoardConnectionRequest,
  WorkItemStatus
} from './board-connection.service';

describe('BoardConnectionService', () => {
  let service: BoardConnectionService;
  let httpTesting: HttpTestingController;

  const storedConnection: BoardConnection = {
    id: 'bc-1',
    provider: 'AzureDevOps',
    organizationUrl: 'https://dev.azure.com/contoso',
    projectName: 'Delivery Platform',
    teamOrAreaPath: null,
    hasStoredCredential: true,
    assigneeFilter: 'RosterMembersOnly',
    contentPolicy: 'MetricsOnly',
    isEnabled: true,
    lastSyncedAtUtc: '2026-10-01T09:00:00Z',
    lastSyncStatus: 'Succeeded',
    lastSyncItemCount: 42,
    stateMapping: { Active: 'InProgress' }
  };

  const baseRequest: SaveBoardConnectionRequest = {
    organizationUrl: 'https://dev.azure.com/contoso',
    projectName: 'Delivery Platform',
    teamOrAreaPath: null,
    assigneeFilter: 'RosterMembersOnly',
    contentPolicy: 'MetricsOnly',
    isEnabled: true
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [BoardConnectionService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(BoardConnectionService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  describe('getConnection', () => {
    it('returns the connection when one is configured', () => {
      let received: BoardConnection | null | undefined;

      service.getConnection().subscribe((connection) => (received = connection));

      httpTesting.expectOne('/api/board-connections').flush(storedConnection);

      expect(received).toEqual(storedConnection);
    });

    it('maps the 204 for an unconfigured squad to null', () => {
      // The distinction the whole screen hangs on. A 204 arriving as an empty object would
      // render as a connection whose organisation URL happens to be blank, which is a
      // different and much more confusing thing than "no board connected".
      let received: BoardConnection | null | undefined = storedConnection;

      service.getConnection().subscribe((connection) => (received = connection));

      httpTesting
        .expectOne('/api/board-connections')
        .flush(null, { status: 204, statusText: 'No Content' });

      expect(received).toBeNull();
    });

    it('propagates a failure rather than reporting no connection', () => {
      // Swallowing this would show "no board connected" to a squad that has one, and invite
      // them to re-enter a token they did not need to re-enter.
      let failure: HttpErrorResponse | undefined;

      service.getConnection().subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

      httpTesting
        .expectOne('/api/board-connections')
        .flush('nope', { status: 500, statusText: 'Server Error' });

      expect(failure?.status).toBe(500);
    });
  });

  describe('saveConnection', () => {
    it('sends the token when the user supplied one', () => {
      service.saveConnection({ ...baseRequest, personalAccessToken: 'pat-123456' }).subscribe();

      const request = httpTesting.expectOne('/api/board-connections');

      expect(request.request.method).toBe('PUT');
      expect(request.request.body.personalAccessToken).toBe('pat-123456');

      request.flush(storedConnection);
    });

    it('omits the token entirely when the field was left blank', () => {
      // Absence is what tells the server to keep the stored credential. Sending null or an
      // empty string would be read the same way today, but omitting it keeps the credential
      // out of the request body altogether.
      service.saveConnection(baseRequest).subscribe();

      const request = httpTesting.expectOne('/api/board-connections');

      expect('personalAccessToken' in request.request.body).toBeFalse();

      request.flush(storedConnection);
    });

    it('sends the state mapping as the numeric enum values the server expects', () => {
      service
        .saveConnection({
          ...baseRequest,
          stateMapping: { 'Code Review': 'PrCreated', Closed: 'Done' }
        })
        .subscribe();

      const request = httpTesting.expectOne('/api/board-connections');

      expect(request.request.body.stateMapping).toEqual({ 'Code Review': 2, Closed: 6 });

      request.flush(storedConnection);
    });

    it('propagates a validation failure to the caller', () => {
      let failure: HttpErrorResponse | undefined;

      service
        .saveConnection(baseRequest)
        .subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

      httpTesting
        .expectOne('/api/board-connections')
        .flush({ title: 'One or more validation errors occurred.' }, {
          status: 400,
          statusText: 'Bad Request'
        });

      expect(failure?.status).toBe(400);
      expect(failure?.error.title).toContain('validation');
    });
  });

  describe('sync', () => {
    it('defaults fullResync to false', () => {
      service.sync().subscribe();

      const request = httpTesting.expectOne(
        (candidate) => candidate.url === '/api/board-connections/sync'
      );

      expect(request.request.params.get('fullResync')).toBe('false');

      request.flush({ importedCount: 0, createdCount: 0, unmappedStates: [], unmatchedAssignees: [], warnings: [], syncedAtUtc: '2026-10-18T00:00:00Z' });
    });

    it('passes fullResync when the full re-read was asked for', () => {
      service.sync(true).subscribe();

      const request = httpTesting.expectOne(
        (candidate) => candidate.url === '/api/board-connections/sync'
      );

      expect(request.request.method).toBe('POST');
      expect(request.request.params.get('fullResync')).toBe('true');

      request.flush({ importedCount: 9, createdCount: 2, unmappedStates: ['Triage'], unmatchedAssignees: [], warnings: [], syncedAtUtc: '2026-10-18T00:00:00Z' });
    });

    it('propagates the 409 raised when no board is connected', () => {
      let failure: HttpErrorResponse | undefined;

      service.sync().subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

      httpTesting
        .expectOne((candidate) => candidate.url === '/api/board-connections/sync')
        .flush({ title: 'No board connected' }, { status: 409, statusText: 'Conflict' });

      expect(failure?.status).toBe(409);
    });
  });

  describe('importFile', () => {
    it('posts the file as multipart under the field name "file"', () => {
      // The controller binds an IFormFile named `file`. Any other field name arrives as null
      // and the upload is rejected as empty, with nothing to indicate why.
      const file = new File(['ID,Title\n1,Thing'], 'query.csv', { type: 'text/csv' });

      service.importFile(file).subscribe();

      const request = httpTesting.expectOne('/api/board-connections/import');
      const body = request.request.body as FormData;

      expect(body instanceof FormData).toBeTrue();
      expect((body.get('file') as File).name).toBe(file.name);

      request.flush({ importedCount: 3, createdCount: 3, unmappedStates: [], unmatchedAssignees: [], warnings: [], syncedAtUtc: '2026-10-18T00:00:00Z' });
    });

    it('leaves the Content-Type unset so the browser adds the multipart boundary', () => {
      const file = new File(['ID,Title'], 'query.csv', { type: 'text/csv' });

      service.importFile(file).subscribe();

      const request = httpTesting.expectOne('/api/board-connections/import');

      expect(request.request.headers.has('Content-Type')).toBeFalse();

      request.flush({ importedCount: 0, createdCount: 0, unmappedStates: [], unmatchedAssignees: [], warnings: [], syncedAtUtc: '2026-10-18T00:00:00Z' });
    });

    it('propagates a rejected upload', () => {
      let failure: HttpErrorResponse | undefined;

      service
        .importFile(new File(['x'], 'notes.txt'))
        .subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

      httpTesting
        .expectOne('/api/board-connections/import')
        .flush({ title: 'Unsupported file type' }, { status: 400, statusText: 'Bad Request' });

      expect(failure?.error.title).toBe('Unsupported file type');
    });
  });

  it('tests the stored credential without sending one', () => {
    service.testConnection().subscribe();

    const request = httpTesting.expectOne('/api/board-connections/test');

    expect(request.request.method).toBe('POST');
    expect(JSON.stringify(request.request.body)).not.toContain('oken');

    request.flush({ succeeded: true, message: null });
  });

  it('deletes the connection', () => {
    service.deleteConnection().subscribe();

    const request = httpTesting.expectOne('/api/board-connections');

    expect(request.request.method).toBe('DELETE');

    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('reads the default state mapping', () => {
    let defaults: Record<string, WorkItemStatus> | undefined;

    service
      .getStateMappingDefaults()
      .subscribe((response) => (defaults = response.azureDevOpsDefaults));

    httpTesting
      .expectOne('/api/board-connections/state-mapping-defaults')
      .flush({ azureDevOpsDefaults: { New: 0, Active: 1, Closed: 6 } });

    expect(defaults).toEqual({ New: 'Backlog', Active: 'InProgress', Closed: 'Done' });
  });

  it('propagates a 403 from any endpoint rather than presenting it as an empty result', () => {
    // These endpoints are Scrum Master only. A developer who reaches the page must be told
    // they are not permitted, not shown a blank form that silently fails to save.
    let failure: HttpErrorResponse | undefined;

    service.testConnection().subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

    httpTesting
      .expectOne('/api/board-connections/test')
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(failure?.status).toBe(403);
  });
});
