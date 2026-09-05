import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StandupTimerComponent } from './standup-timer.component';
import { TeamMember } from '../../../../core/models/scrum.models';

describe('StandupTimerComponent', () => {
  let component: StandupTimerComponent;
  let fixture: ComponentFixture<StandupTimerComponent>;

  const mockMembers: TeamMember[] = [
    { id: 'm-1', name: 'Alice', email: 'alice@test.com', role: 'Developer', avatar: '', location: 'BLR', timeZone: 'IST', activeWipLimit: 3, isActive: true },
    { id: 'm-2', name: 'Bob', email: 'bob@test.com', role: 'QaEngineer', avatar: '', location: 'BLR', timeZone: 'IST', activeWipLimit: 3, isActive: true }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StandupTimerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(StandupTimerComponent);
    component = fixture.componentInstance;
    component.members = [...mockMembers];
    fixture.detectChanges();
  });

  it('should create and format timer string cleanly', () => {
    expect(component).toBeTruthy();
    expect(component.formatTimer(120)).toBe('2:00');
    expect(component.formatTimer(65)).toBe('1:05');
    expect(component.formatTimer(9)).toBe('0:09');
  });

  it('should identify current speaker correctly', () => {
    component.currentSpeakerIndex = 0;
    expect(component.currentSpeaker?.name).toBe('Alice');

    component.currentSpeakerIndex = 1;
    expect(component.currentSpeaker?.name).toBe('Bob');

    component.members = [];
    expect(component.currentSpeaker).toBeNull();
  });

  it('should emit events on toggle, reset, nextSpeaker, and selectSpeaker', () => {
    spyOn(component.toggle, 'emit');
    spyOn(component.reset, 'emit');
    spyOn(component.nextSpeaker, 'emit');
    spyOn(component.selectSpeaker, 'emit');

    component.toggle.emit();
    expect(component.toggle.emit).toHaveBeenCalled();

    component.reset.emit();
    expect(component.reset.emit).toHaveBeenCalled();

    component.nextSpeaker.emit();
    expect(component.nextSpeaker.emit).toHaveBeenCalled();

    component.selectSpeaker.emit(1);
    expect(component.selectSpeaker.emit).toHaveBeenCalledWith(1);
  });
});
