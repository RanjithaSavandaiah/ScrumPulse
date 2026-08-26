import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { ScrumStateService } from './scrum-state.service';
import { appReducers } from '../state';

describe('ScrumStateService (Modular NgRx Facade)', () => {
  let service: ScrumStateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    });
    service = TestBed.inject(ScrumStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should expose reactive signals for modular state slices', () => {
    expect(service.sprints()).toEqual([]);
    expect(service.members()).toEqual([]);
    expect(service.workItems()).toEqual([]);
    expect(service.prLogs()).toEqual([]);
    expect(service.currentRole()).toBe('ScrumMaster');
    expect(service.isDarkMode()).toBeTrue();
  });

  it('should compute sayDoRatio default value', () => {
    expect(service.sayDoRatio()).toBeGreaterThanOrEqual(0);
  });
});
