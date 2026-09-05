import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { of, throwError } from 'rxjs';
import { AiCoachComponent } from './ai-coach.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { AiSuggestionResponse, CopilotChatResponse } from '../../core/models/scrum.models';

describe('AiCoachComponent', () => {
  let component: AiCoachComponent;
  let fixture: ComponentFixture<AiCoachComponent>;
  let stateService: ScrumStateService;

  const mockAiSuggestion: AiSuggestionResponse = {
    level: 'Individual',
    title: 'High Velocity & Low Pickup Latency',
    summary: 'Consistently picking up ready tasks quickly',
    keyFindings: ['0.8h average pickup latency', 'High unit test coverage'],
    actionableRecommendations: ['Consider mentoring junior engineers on pull request reviews'],
    riskLevel: 'Low',
    generatedAtUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiCoachComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AiCoachComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create and initialize with default individual tier', () => {
    expect(component).toBeTruthy();
    expect(component.aiTier).toBe('individual');
    expect(component.chatMessages.length).toBeGreaterThan(0);
  });

  it('should switch AI tiers and fetch relevant suggestions', () => {
    spyOn(stateService, 'getIndividualAi').and.returnValue(of(mockAiSuggestion));
    spyOn(stateService, 'getProjectAi').and.returnValue(of({ ...mockAiSuggestion, level: 'Project' }));
    spyOn(stateService, 'getCompanyAi').and.returnValue(of({ ...mockAiSuggestion, level: 'Company' }));

    component.selectedMemberId = 'm1';
    component.loadAiSuggestion('individual');
    expect(stateService.getIndividualAi).toHaveBeenCalledWith('m1');
    expect(component.aiData?.level).toBe('Individual');

    component.selectedSprintId = 's1';
    component.loadAiSuggestion('project');
    expect(stateService.getProjectAi).toHaveBeenCalledWith('s1');
    expect(component.aiData?.level).toBe('Project');

    component.loadAiSuggestion('company');
    expect(stateService.getCompanyAi).toHaveBeenCalled();
    expect(component.aiData?.level).toBe('Company');
  });

  it('should handle member and sprint filter changes', () => {
    spyOn(stateService, 'getIndividualAi').and.returnValue(of(mockAiSuggestion));
    spyOn(stateService, 'getProjectAi').and.returnValue(of(mockAiSuggestion));

    component.onMemberChange('m2');
    expect(component.selectedMemberId).toBe('m2');
    expect(stateService.getIndividualAi).toHaveBeenCalledWith('m2');

    component.onSprintChange('s2');
    expect(component.selectedSprintId).toBe('s2');
    expect(stateService.getProjectAi).toHaveBeenCalledWith('s2');
  });

  it('should send chat prompt and append Copilot response', () => {
    const mockReply: CopilotChatResponse = {
      answer: 'Sprint 34 burn-down is on track with 92% say-do ratio.',
      suggestedFollowUps: ['Review blockers', 'Check PR backlog'],
      timestampUtc: new Date().toISOString()
    };
    spyOn(stateService, 'askCopilot').and.returnValue(of(mockReply));

    const initialLen = component.chatMessages.length;
    component.sendChat('What is the sprint status?');

    expect(stateService.askCopilot).toHaveBeenCalled();
    expect(component.chatMessages.length).toBe(initialLen + 2);
    expect(component.chatMessages[component.chatMessages.length - 1].text).toBe(mockReply.answer);
  });

  it('should handle Copilot chat error gracefully with user-friendly error message', () => {
    spyOn(stateService, 'askCopilot').and.returnValue(throwError(() => new Error('Server timeout')));

    const initialLen = component.chatMessages.length;
    component.sendChat('Trigger error');

    expect(component.chatMessages.length).toBe(initialLen + 2);
    expect(component.chatMessages[component.chatMessages.length - 1].text).toContain('Sorry, I encountered an issue');
  });
});
