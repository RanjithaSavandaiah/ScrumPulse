import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  BoardAssigneeFilter,
  BoardConnection,
  ContentSensitivityPolicy,
  WorkItemStatus
} from '../../core/services/board-connection.service';
import { BoardConnectionComponent } from './board-connection.component';

describe('BoardConnectionComponent', () => {
  let fixture: ComponentFixture<BoardConnectionComponent>;
  let httpTesting: HttpTestingController;

  const connectedSquad: BoardConnection = {
    id: 'bc-1',
    provider: 'AzureDevOps',
    organizationUrl: 'https://dev.azure.com/contoso',
    projectName: 'Delivery Platform',
    teamOrAreaPath: 'Delivery Platform\\Payments',
    hasStoredCredential: true,
    assigneeFilter: 'RosterMembersOnly',
    contentPolicy: 'MetricsOnly',
    isEnabled: true,
    lastSyncedAtUtc: '2026-10-01T09:00:00Z',
    lastSyncStatus: 'Succeeded',
    lastSyncItemCount: 42,
    stateMapping: { Active: 'InProgress', Closed: 'Done' }
  };

  function element<T extends HTMLElement>(testId: string): T | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as T | null;
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  /** Settles the initial GET as either a configured squad or a 204. */
  async function load(connection: BoardConnection | null): Promise<void> {
    const request = httpTesting.expectOne('/api/board-connections');

    if (connection) {
      request.flush(connection);
    } else {
      request.flush(null, { status: 204, statusText: 'No Content' });
      httpTesting
        .expectOne('/api/board-connections/state-mapping-defaults')
        .flush({ azureDevOpsDefaults: { New: 0, Active: 1, Closed: 6 } });
    }

    fixture.detectChanges();
    await fixture.whenStable();
  }

  function typeInto(testId: string, value: string): void {
    const input = element<HTMLInputElement>(testId)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BoardConnectionComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(BoardConnectionComponent);
    fixture.detectChanges();
  });

  afterEach(() => httpTesting.verify());

  it('shows no connection for a squad that has not configured one', async () => {
    await load(null);

    expect(text()).toContain('No board connected');
  });

  it('shows the last sync detail for a connected squad', async () => {
    await load(connectedSquad);

    const status = element('connection-status')!;

    expect(status.textContent).toContain('Connected');
    expect(status.textContent).toContain('Delivery Platform');
    expect(status.textContent).toContain('Succeeded');
    expect(status.textContent).toContain('42');
  });

  describe('validation', () => {
    it('blocks the save and names the missing fields rather than posting a blank form', async () => {
      await load(null);

      element<HTMLButtonElement>('save-connection')!.click();
      fixture.detectChanges();

      // No request is the assertion: verify() in afterEach fails if one went out.
      expect(text()).toContain('Organisation URL is required.');
      expect(text()).toContain('Project name is required.');
      expect(element('error-banner')?.textContent).toContain('Fix the highlighted fields');
    });

    it('rejects a non-https organisation URL in the browser', async () => {
      // Caught here as well as on the server so the mistake is explained while the user is
      // still looking at the field, rather than coming back as a 400 after a round trip.
      await load(null);

      typeInto('organization-url', 'http://dev.azure.com/contoso');
      typeInto('project-name', 'Delivery Platform');

      element<HTMLButtonElement>('save-connection')!.click();
      fixture.detectChanges();

      expect(text()).toContain('must start with https://');
    });

    it('refuses a first save with no token, since there would be nothing to authenticate with', async () => {
      await load(null);

      typeInto('organization-url', 'https://dev.azure.com/contoso');
      typeInto('project-name', 'Delivery Platform');

      element<HTMLButtonElement>('save-connection')!.click();
      fixture.detectChanges();

      expect(text()).toContain('personal access token is needed the first time');
    });
  });

  describe('the stored credential', () => {
    it('leaves the token field blank and says so, because the API never returns one', async () => {
      await load(connectedSquad);

      const token = element<HTMLInputElement>('personal-access-token')!;

      expect(token.type).toBe('password');
      expect(token.value).toBe('');
      expect(token.placeholder).toContain('Leave blank to keep');
      expect(token.autocomplete).toBe('off');
    });

    it('omits the token from the save when the field was left blank', async () => {
      await load(connectedSquad);

      typeInto('project-name', 'Delivery Platform Renamed');
      element<HTMLButtonElement>('save-connection')!.click();

      const request = httpTesting.expectOne('/api/board-connections');

      expect(request.request.method).toBe('PUT');
      expect('personalAccessToken' in request.request.body).toBeFalse();
      expect(request.request.body.projectName).toBe('Delivery Platform Renamed');

      request.flush({ ...connectedSquad, projectName: 'Delivery Platform Renamed' });
    });

    it('sends a replacement token when one is typed', async () => {
      await load(connectedSquad);

      typeInto('personal-access-token', 'replacement-token');
      element<HTMLButtonElement>('save-connection')!.click();

      const request = httpTesting.expectOne('/api/board-connections');

      expect(request.request.body.personalAccessToken).toBe('replacement-token');

      request.flush(connectedSquad);
    });
  });

  describe('sync results', () => {
    it('lists unmapped states and unmatched assignees instead of counting them', async () => {
      await load(connectedSquad);

      element<HTMLButtonElement>('sync-now')!.click();

      httpTesting.expectOne((candidate) => candidate.url === '/api/board-connections/sync').flush({
        importedCount: 18,
        createdCount: 4,
        unmappedStates: ['Triage', 'Awaiting Client'],
        unmatchedAssignees: ['priya.k@contoso.com'],
        warnings: ['Two items had no story points.'],
        syncedAtUtc: '2026-10-18T10:00:00Z'
      });

      // The sync refreshes the status panel, so the GET must be settled too.
      httpTesting.expectOne('/api/board-connections').flush(connectedSquad);
      fixture.detectChanges();

      const unmapped = element('unmapped-states')!;
      expect(unmapped.textContent).toContain('Triage');
      expect(unmapped.textContent).toContain('Awaiting Client');

      const unmatched = element('unmatched-assignees')!;
      expect(unmatched.textContent).toContain('priya.k@contoso.com');

      expect(element('sync-warnings')?.textContent).toContain('no story points');
      expect(element('sync-result')?.textContent).toContain('18');
    });

    it('passes fullResync when the full re-read is chosen', async () => {
      await load(connectedSquad);

      element<HTMLButtonElement>('full-resync')!.click();

      const request = httpTesting.expectOne(
        (candidate) => candidate.url === '/api/board-connections/sync'
      );

      expect(request.request.params.get('fullResync')).toBe('true');

      request.flush({
        importedCount: 0,
        createdCount: 0,
        unmappedStates: [],
        unmatchedAssignees: [],
        warnings: [],
        syncedAtUtc: '2026-10-18T10:00:00Z'
      });

      httpTesting.expectOne('/api/board-connections').flush(connectedSquad);
    });
  });

  describe('deleting the connection', () => {
    it('asks before destroying the stored token', async () => {
      await load(connectedSquad);

      element<HTMLButtonElement>('delete-connection')!.click();
      fixture.detectChanges();

      // Nothing has been sent: verify() would fail on a stray DELETE.
      expect(element('delete-confirm')).not.toBeNull();
      expect(element('delete-confirm')!.textContent).toContain('destroys the stored token');
    });

    it('deletes only after the confirmation is accepted', async () => {
      await load(connectedSquad);

      element<HTMLButtonElement>('delete-connection')!.click();
      fixture.detectChanges();
      element<HTMLButtonElement>('delete-confirm-yes')!.click();

      const request = httpTesting.expectOne('/api/board-connections');
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });

      // Resetting the form drops back to the defaults endpoint for a blank mapping grid.
      httpTesting
        .expectOne('/api/board-connections/state-mapping-defaults')
        .flush({ azureDevOpsDefaults: { New: 0 } });
      fixture.detectChanges();

      expect(text()).toContain('No board connected');
    });

    it('keeps the connection when the confirmation is declined', async () => {
      await load(connectedSquad);

      element<HTMLButtonElement>('delete-connection')!.click();
      fixture.detectChanges();
      element<HTMLButtonElement>('delete-confirm-no')!.click();
      fixture.detectChanges();

      expect(element('delete-confirm')).toBeNull();
      expect(element('connection-status')!.textContent).toContain('Connected');
    });
  });

  describe('state mapping', () => {
    it('lists the connection\'s own board states', async () => {
      await load(connectedSquad);

      const mapping = element('state-mapping')!;

      expect(mapping.textContent).toContain('Active');
      expect(mapping.textContent).toContain('Closed');
    });

    it('falls back to the defaults endpoint when the connection carries no mapping', async () => {
      const request = httpTesting.expectOne('/api/board-connections');
      request.flush({ ...connectedSquad, stateMapping: {} });

      httpTesting
        .expectOne('/api/board-connections/state-mapping-defaults')
        .flush({ azureDevOpsDefaults: { New: 0, 'Code Review': 2 } });

      fixture.detectChanges();

      expect(element('state-mapping')!.textContent).toContain('Code Review');
    });

    it('sends the edited mapping with the save', async () => {
      await load(connectedSquad);

      const select = element('state-mapping')!.querySelector('select') as HTMLSelectElement;
      select.value = 'InQa';
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      element<HTMLButtonElement>('save-connection')!.click();

      const request = httpTesting.expectOne('/api/board-connections');

      expect([5, 'InQa'] as unknown[]).toContain(request.request.body.stateMapping['Active']);

      request.flush(connectedSquad);
    });
  });

  it('explains the content policy on screen rather than only in a tooltip', async () => {
    await load(null);

    const note = element('content-policy-note')!;

    expect(note.textContent).toContain('Metrics only is the default');
    expect(note.textContent).toContain('client');
  });

  it('surfaces the server\'s explanation when a save is rejected', async () => {
    await load(connectedSquad);

    element<HTMLButtonElement>('save-connection')!.click();

    httpTesting.expectOne('/api/board-connections').flush(
      { title: 'One or more validation errors occurred.', detail: 'Project name was not found.' },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(element('error-banner')!.textContent).toContain('Project name was not found.');
  });

  it('surfaces Scrum Master PIN requirement when API returns 403 Forbidden', async () => {
    const request = httpTesting.expectOne('/api/board-connections');
    request.flush(
      { title: 'Forbidden', detail: 'Scrum Master authorization required.' },
      { status: 403, statusText: 'Forbidden' }
    );
    fixture.detectChanges();
    await fixture.whenStable();

    expect(element('error-banner')!.textContent).toContain('Scrum Master role required');
  });

  it('tests the stored personal access token and shows the test result', async () => {
    await load(connectedSquad);

    element<HTMLButtonElement>('test-connection')!.click();

    const request = httpTesting.expectOne('/api/board-connections/test');
    expect(request.request.method).toBe('POST');
    request.flush({ succeeded: true, message: 'Azure DevOps accepted the stored token.' });
    fixture.detectChanges();

    const result = element('test-result')!;
    expect(result.textContent).toContain('Azure DevOps accepted the stored token.');
  });

  it('imports a CSV query export file and refreshes status', async () => {
    await load(connectedSquad);

    const fileInput = element<HTMLInputElement>('import-file')!;
    const mockFile = new File(['ID,Title,State\n101,Test Task,Active\n'], 'export.csv', { type: 'text/csv' });
    Object.defineProperty(fileInput, 'files', { value: [mockFile] });
    fileInput.dispatchEvent(new Event('change'));

    const request = httpTesting.expectOne('/api/board-connections/import');
    expect(request.request.method).toBe('POST');
    request.flush({
      importedCount: 1,
      createdCount: 1,
      unmappedStates: [],
      unmatchedAssignees: [],
      warnings: [],
      syncedAtUtc: '2026-10-19T10:00:00Z'
    });

    httpTesting.expectOne('/api/board-connections').flush(connectedSquad);
    fixture.detectChanges();

    expect(element('sync-result')?.textContent).toContain('1');
  });

  it('displays unmapped states and unmatched assignees callouts when importing CSV', async () => {
    await load(connectedSquad);

    const fileInput = element<HTMLInputElement>('import-file')!;
    const mockFile = new File(['ID,Title,State,Assigned To\n201,Task,Draft,external@partner.com\n'], 'export.csv', { type: 'text/csv' });
    Object.defineProperty(fileInput, 'files', { value: [mockFile] });
    fileInput.dispatchEvent(new Event('change'));

    const request = httpTesting.expectOne('/api/board-connections/import');
    request.flush({
      importedCount: 1,
      createdCount: 1,
      unmappedStates: ['Draft'],
      unmatchedAssignees: ['external@partner.com'],
      warnings: ['Custom rule applied'],
      syncedAtUtc: '2026-10-19T10:00:00Z'
    });

    httpTesting.expectOne('/api/board-connections').flush(connectedSquad);
    fixture.detectChanges();

    expect(element('unmapped-states')?.textContent).toContain('Draft');
    expect(element('unmatched-assignees')?.textContent).toContain('external@partner.com');
    expect(element('sync-warnings')?.textContent).toContain('Custom rule applied');
  });

  it('displays error banner when file import endpoint returns failure', async () => {
    await load(connectedSquad);

    const fileInput = element<HTMLInputElement>('import-file')!;
    const mockFile = new File(['bad content'], 'corrupted.csv', { type: 'text/csv' });
    Object.defineProperty(fileInput, 'files', { value: [mockFile] });
    fileInput.dispatchEvent(new Event('change'));

    const request = httpTesting.expectOne('/api/board-connections/import');
    request.flush(
      { title: 'Bad Request', detail: 'Only a .csv export is supported.' },
      { status: 400, statusText: 'Bad Request' }
    );
    fixture.detectChanges();

    expect(element('error-banner')?.textContent).toContain('Only a .csv export is supported.');
  });

  it('displays error banner when live sync endpoint fails', async () => {
    await load(connectedSquad);

    element<HTMLButtonElement>('sync-now')!.click();
    fixture.detectChanges();

    const request = httpTesting.expectOne((req) => req.url === '/api/board-connections/sync');
    request.flush(
      { title: 'Conflict', detail: 'Could not contact Azure DevOps.' },
      { status: 409, statusText: 'Conflict' }
    );
    fixture.detectChanges();

    expect(element('error-banner')?.textContent).toContain('Could not contact Azure DevOps.');
  });

  it('does nothing when file input change event contains no files', async () => {
    await load(connectedSquad);

    const fileInput = element<HTMLInputElement>('import-file')!;
    Object.defineProperty(fileInput, 'files', { value: [] });
    fileInput.dispatchEvent(new Event('change'));

    // No POST to /api/board-connections/import should occur
    httpTesting.expectNone('/api/board-connections/import');
    expect(element('error-banner')).toBeNull();
    expect(element('sync-result')).toBeNull();
  });
});
