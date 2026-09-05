import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AiInsightsCardComponent } from './ai-insights-card.component';
import { AiSuggestionResponse } from '../../../../core/models/scrum.models';

describe('AiInsightsCardComponent', () => {
  let component: AiInsightsCardComponent;
  let fixture: ComponentFixture<AiInsightsCardComponent>;

  const mockAiData: AiSuggestionResponse = {
    level: 'Individual',
    title: 'Individual Developer Productivity',
    summary: 'Strong focus on PR reviews',
    keyFindings: ['High review speed', 'Active collaboration'],
    actionableRecommendations: ['Increase automated unit test coverage', 'Decompose large user stories'],
    riskLevel: 'Low',
    generatedAtUtc: '2026-09-05T10:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiInsightsCardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AiInsightsCardComponent);
    component = fixture.componentInstance;
    component.aiData = mockAiData;
    fixture.detectChanges();
  });

  it('should create and accept input signals', () => {
    expect(component).toBeTruthy();
    expect(component.aiTier).toBe('individual');
    expect(component.aiData?.actionableRecommendations.length).toBe(2);
  });

  it('should emit tierChange, memberChange, and sprintChange', () => {
    spyOn(component.tierChange, 'emit');
    spyOn(component.memberChange, 'emit');
    spyOn(component.sprintChange, 'emit');

    component.tierChange.emit('project');
    expect(component.tierChange.emit).toHaveBeenCalledWith('project');

    component.memberChange.emit('mem-1');
    expect(component.memberChange.emit).toHaveBeenCalledWith('mem-1');

    component.sprintChange.emit('sp-1');
    expect(component.sprintChange.emit).toHaveBeenCalledWith('sp-1');
  });
});
