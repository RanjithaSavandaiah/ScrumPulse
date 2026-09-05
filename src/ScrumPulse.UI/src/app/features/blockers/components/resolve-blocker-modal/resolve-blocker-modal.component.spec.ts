import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ResolveBlockerModalComponent } from './resolve-blocker-modal.component';
import { Blocker } from '../../../../core/models/scrum.models';

describe('ResolveBlockerModalComponent', () => {
  let component: ResolveBlockerModalComponent;
  let fixture: ComponentFixture<ResolveBlockerModalComponent>;

  const mockBlocker: Blocker = {
    id: 'b-1',
    title: 'Postgres staging connection failed',
    description: '',
    category: 'EnvironmentAccess',
    slaHoursLimit: 8,
    raisedById: 'm-1',
    raisedAtUtc: new Date().toISOString(),
    isResolved: false,
    hoursWaiting: 2,
    isSlaBreached: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ResolveBlockerModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ResolveBlockerModalComponent);
    component = fixture.componentInstance;
    component.blocker = mockBlocker;
    fixture.detectChanges();
  });

  it('should create and set preset notes', () => {
    expect(component).toBeTruthy();
    expect(component.presets.length).toBeGreaterThan(0);

    component.setPreset(component.presets[0]);
    expect(component.notes).toBe(component.presets[0]);
  });

  it('should emit resolve event on confirmation', () => {
    spyOn(component.resolve, 'emit');

    component.notes = 'Fixed credentials';
    component.onConfirm();

    expect(component.resolve.emit).toHaveBeenCalledWith({
      id: 'b-1',
      notes: 'Fixed credentials'
    });
  });
});
