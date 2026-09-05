import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { BlockersComponent } from './blockers.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { Blocker } from '../../core/models/scrum.models';

describe('BlockersComponent', () => {
  let component: BlockersComponent;
  let fixture: ComponentFixture<BlockersComponent>;
  let stateService: ScrumStateService;

  const mockBlocker: Blocker = {
    id: 'b1',
    sprintId: 's1',
    title: 'VPN timeout on staging server',
    description: 'Cannot reach DB instance',
    category: 'EnvironmentAccess',
    raisedById: 'm1',
    raisedByName: 'Alice',
    slaHoursLimit: 8,
    isResolved: false,
    isSlaBreached: false,
    raisedAtUtc: new Date().toISOString(),
    hoursWaiting: 2
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BlockersComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BlockersComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should manage create and edit modal lifecycle', () => {
    expect(component.showNewBlockerModal()).toBeFalse();
    expect(component.selectedBlockerForEdit()).toBeNull();

    component.onOpenCreateModal();
    expect(component.showNewBlockerModal()).toBeTrue();
    expect(component.selectedBlockerForEdit()).toBeNull();

    component.onOpenEditModal(mockBlocker);
    expect(component.showNewBlockerModal()).toBeTrue();
    expect(component.selectedBlockerForEdit()).toBe(mockBlocker);

    component.onCloseBlockerModal();
    expect(component.showNewBlockerModal()).toBeFalse();
    expect(component.selectedBlockerForEdit()).toBeNull();
  });

  it('should dispatch createBlocker when saving new blocker', () => {
    spyOn(stateService, 'createBlocker');

    component.onOpenCreateModal();
    component.onSaveBlocker({
      title: 'Missing API Swagger definition',
      description: 'Need contract for client endpoints',
      category: 3,
      slaHoursLimit: 24
    });

    expect(stateService.createBlocker).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'Missing API Swagger definition',
      category: 3,
      slaHoursLimit: 24
    }));
    expect(component.showNewBlockerModal()).toBeFalse();
  });

  it('should dispatch updateBlocker when editing existing blocker', () => {
    spyOn(stateService, 'updateBlocker');

    component.onOpenEditModal(mockBlocker);
    component.onSaveBlocker({
      title: 'VPN timeout on staging server - updated',
      description: 'Issue persists after restart',
      category: 2,
      slaHoursLimit: 12
    });

    expect(stateService.updateBlocker).toHaveBeenCalledWith('b1', jasmine.objectContaining({
      title: 'VPN timeout on staging server - updated',
      sprintId: 's1'
    }));
    expect(component.showNewBlockerModal()).toBeFalse();
  });

  it('should handle blocker resolution lifecycle', () => {
    spyOn(stateService, 'resolveBlocker');

    component.onOpenResolveModal(mockBlocker);
    expect(component.selectedBlockerForResolution()).toBe(mockBlocker);

    component.onConfirmResolve({ id: 'b1', notes: 'Whitelisted IP in staging security group' });
    expect(stateService.resolveBlocker).toHaveBeenCalledWith('b1', 'Whitelisted IP in staging security group');
    expect(component.selectedBlockerForResolution()).toBeNull();
  });

  it('should handle deletion flows', () => {
    spyOn(stateService, 'deleteBlocker');

    component.onPromptDeleteBlocker(mockBlocker);
    expect(component.blockerToDelete()).toBe(mockBlocker);

    component.onConfirmDeleteBlocker();
    expect(stateService.deleteBlocker).toHaveBeenCalledWith('b1');
    expect(component.blockerToDelete()).toBeNull();

    // Modal direct delete
    component.onDeleteFromModal('b1');
    expect(stateService.deleteBlocker).toHaveBeenCalledWith('b1');
  });
});
