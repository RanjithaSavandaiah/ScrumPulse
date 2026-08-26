import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { BlockersComponent } from './blockers.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('BlockersComponent', () => {
  let component: BlockersComponent;
  let fixture: ComponentFixture<BlockersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BlockersComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(BlockersComponent);
    component = fixture.componentInstance;
  });

  it('should create and toggle blocker modal', () => {
    expect(component).toBeTruthy();
    expect(component.showNewBlockerModal()).toBeFalse();

    component.showNewBlockerModal.set(true);
    expect(component.showNewBlockerModal()).toBeTrue();
  });
});
