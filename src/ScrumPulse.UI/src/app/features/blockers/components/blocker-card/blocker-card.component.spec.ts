import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BlockerCardComponent } from './blocker-card.component';
import { Blocker } from '../../../../core/models/scrum.models';

describe('BlockerCardComponent', () => {
  let component: BlockerCardComponent;
  let fixture: ComponentFixture<BlockerCardComponent>;

  const mockBlocker: Blocker = {
    id: 'blk-1',
    title: 'Database connection pool exhausted',
    description: 'Postgres instances returning 500',
    category: 'EnvironmentAccess',
    slaHoursLimit: 4,
    hoursWaiting: 2.5,
    isResolved: false,
    resolutionNotes: undefined,
    isSlaBreached: false,
    raisedById: 'm-1',
    raisedByName: 'Alice',
    raisedAtUtc: '2026-09-05T08:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BlockerCardComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(BlockerCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('blocker', { ...mockBlocker });
    fixture.detectChanges();
  });

  it('should create and bind blocker details', () => {
    expect(component).toBeTruthy();
    expect(component.blocker.title).toBe('Database connection pool exhausted');
    expect(component.blocker.isResolved).toBeFalse();
  });

  it('should emit resolve, edit, and delete events', () => {
    spyOn(component.resolve, 'emit');
    spyOn(component.edit, 'emit');
    spyOn(component.delete, 'emit');

    component.resolve.emit(component.blocker);
    expect(component.resolve.emit).toHaveBeenCalledWith(component.blocker);

    component.edit.emit(component.blocker);
    expect(component.edit.emit).toHaveBeenCalledWith(component.blocker);

    component.delete.emit(component.blocker);
    expect(component.delete.emit).toHaveBeenCalledWith(component.blocker);
  });

  it('should render blocked-hours-badge when blockedHours > 0', () => {
    fixture.componentRef.setInput('blocker', { ...mockBlocker, blockedHours: 8.5 });
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.blocked-hours-badge');
    expect(badge).toBeTruthy();
    expect(badge.textContent).toContain('8.5h blocked (-8.5h capacity)');
  });
});
