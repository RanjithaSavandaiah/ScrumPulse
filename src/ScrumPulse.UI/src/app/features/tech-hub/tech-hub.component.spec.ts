import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { TechHubComponent } from './tech-hub.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { TechDebtItem, TechTalkLog } from '../../core/models/scrum.models';

describe('TechHubComponent', () => {
  let component: TechHubComponent;
  let fixture: ComponentFixture<TechHubComponent>;
  let stateService: ScrumStateService;

  const mockDebt: TechDebtItem = {
    id: 'td-1',
    title: 'Migrate legacy auth to OAuth 2.1 PKCE',
    description: 'Deprecate legacy session tokens',
    severity: 'High',
    estimatedHours: 16,
    status: 'Identified',
    payoffSprintId: 's1',
    assigneeId: 'm1',
    assigneeName: 'Alice'
  };

  const mockTalk: TechTalkLog = {
    id: 'tt-1',
    topic: 'Microfrontends with Angular 22 & Native Federation',
    presenterId: 'm1',
    presenterName: 'Alice',
    talkDate: '2026-09-18',
    durationMinutes: 45,
    keyTakeaways: 'Native federation eliminates webpack lock-in',
    slidesUrl: 'https://slides.internal/mfe'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TechHubComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TechHubComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create successfully', () => {
    expect(component).toBeTruthy();
  });

  it('should manage tech debt modal opening and closing', () => {
    expect(component.isTechDebtModalOpen()).toBeFalse();
    component.openCreateTechDebt();
    expect(component.isTechDebtModalOpen()).toBeTrue();
    expect(component.selectedTechDebt()).toBeNull();

    component.openEditTechDebt(mockDebt);
    expect(component.isTechDebtModalOpen()).toBeTrue();
    expect(component.selectedTechDebt()).toBe(mockDebt);

    component.closeTechDebtModal();
    expect(component.isTechDebtModalOpen()).toBeFalse();
    expect(component.selectedTechDebt()).toBeNull();
  });

  it('should dispatch create or update tech debt', () => {
    spyOn(stateService, 'createTechDebt');
    spyOn(stateService, 'updateTechDebt');

    // Create
    component.saveTechDebt({ title: 'Upgrade NSwag', severity: 'Medium' });
    expect(stateService.createTechDebt).toHaveBeenCalledWith({ title: 'Upgrade NSwag', severity: 'Medium' });

    // Update
    component.saveTechDebt({ id: 'td-1', title: 'Upgrade NSwag v14', severity: 'High' });
    expect(stateService.updateTechDebt).toHaveBeenCalledWith('td-1', jasmine.objectContaining({ title: 'Upgrade NSwag v14' }));
  });

  it('should manage tech talk modal and saving', () => {
    spyOn(stateService, 'createTechTalk');
    spyOn(stateService, 'updateTechTalk');

    component.openCreateTechTalk();
    expect(component.isTechTalkModalOpen()).toBeTrue();

    component.saveTechTalk({ topic: 'Deep dive into Signals' });
    expect(stateService.createTechTalk).toHaveBeenCalledWith({ topic: 'Deep dive into Signals' });
    expect(component.isTechTalkModalOpen()).toBeFalse();

    component.openEditTechTalk(mockTalk);
    component.saveTechTalk({ id: 'tt-1', topic: 'Signals in depth' });
    expect(stateService.updateTechTalk).toHaveBeenCalledWith('tt-1', jasmine.objectContaining({ topic: 'Signals in depth' }));
  });

  it('should handle tech debt resolve toggle', () => {
    spyOn(stateService, 'resolveTechDebt');
    const mockEvent = new MouseEvent('click');
    spyOn(mockEvent, 'stopPropagation');

    component.toggleResolveTechDebt(mockDebt, mockEvent);
    expect(mockEvent.stopPropagation).toHaveBeenCalled();
    expect(stateService.resolveTechDebt).toHaveBeenCalledWith('td-1', 'Resolved');

    const resolvedDebt = { ...mockDebt, status: 'Resolved' };
    component.toggleResolveTechDebt(resolvedDebt, mockEvent);
    expect(stateService.resolveTechDebt).toHaveBeenCalledWith('td-1', 'Identified');
  });

  it('should resolve presenter and sprint names cleanly', () => {
    expect(component.getPresenterName(mockTalk)).toBe('Alice');
    expect(component.getPresenterName({ ...mockTalk, presenterName: undefined, presenterId: 'nonexistent' })).toBe('Team Member');
    expect(component.getSprintName(undefined)).toBe('Unassigned Backlog');
  });
});
