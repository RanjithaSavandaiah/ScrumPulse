import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { CapacityComponent } from './capacity.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('CapacityComponent', () => {
  let component: CapacityComponent;
  let fixture: ComponentFixture<CapacityComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CapacityComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(CapacityComponent);
    component = fixture.componentInstance;
  });

  it('should create and toggle leave modal', () => {
    expect(component).toBeTruthy();
    expect(component.showLeaveModal()).toBeFalse();

    component.showLeaveModal.set(true);
    expect(component.showLeaveModal()).toBeTrue();
  });
});
