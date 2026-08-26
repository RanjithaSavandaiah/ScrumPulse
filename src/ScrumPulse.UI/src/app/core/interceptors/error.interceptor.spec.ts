import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should intercept 500 error and transform into informative Error message', (done) => {
    httpClient.get('/api/test-endpoint').subscribe({
      next: () => fail('should have failed with 500'),
      error: (error: Error) => {
        expect(error).toBeTruthy();
        expect(error.message).toContain('Database connection timeout');
        done();
      }
    });

    const req = httpTestingController.expectOne('/api/test-endpoint');
    req.flush(
      { title: 'Server Error', detail: 'Database connection timeout' },
      { status: 500, statusText: 'Internal Server Error' }
    );
  });

  it('should handle network connection failure status 0', (done) => {
    httpClient.get('/api/network-offline').subscribe({
      next: () => fail('should have failed with network error'),
      error: (error: Error) => {
        expect(error).toBeTruthy();
        expect(error.message).toContain('Unable to connect to ScrumPulse API server');
        done();
      }
    });

    const req = httpTestingController.expectOne('/api/network-offline');
    req.error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
  });
});
