import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { tenantInterceptor } from './tenant.interceptor';

describe('tenantInterceptor', () => {
  let httpClient: HttpClient;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tenantInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
    localStorage.clear();
  });

  it('should attach default ScrumMaster role headers when localStorage is empty', (done) => {
    httpClient.get('/api/sprints').subscribe(response => {
      expect(response).toBeTruthy();
      done();
    });

    const req = httpTestingController.expectOne('/api/sprints');
    expect(req.request.headers.get('X-User-Role')).toBe('ScrumMaster');
    expect(req.request.headers.get('X-User-Name')).toBe('ScrumMaster');
    expect(req.request.headers.has('X-Team-Id')).toBeFalse();
    req.flush([]);
  });

  it('should attach custom role and team id headers from localStorage', (done) => {
    localStorage.setItem('scrumpulse_current_role', 'Developer');
    localStorage.setItem('scrumpulse_current_team_id', 'squad-alpha-123');

    httpClient.get('/api/work-items').subscribe(response => {
      expect(response).toBeTruthy();
      done();
    });

    const req = httpTestingController.expectOne('/api/work-items');
    expect(req.request.headers.get('X-User-Role')).toBe('Developer');
    expect(req.request.headers.get('X-User-Name')).toBe('Developer');
    expect(req.request.headers.get('X-Team-Id')).toBe('squad-alpha-123');
    req.flush([]);
  });
});
