import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ExecutiveComponent } from './executive.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('ExecutiveComponent', () => {
  let component: ExecutiveComponent;
  let fixture: ComponentFixture<ExecutiveComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExecutiveComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(ExecutiveComponent);
    component = fixture.componentInstance;
  });

  it('should create successfully', () => {
    expect(component).toBeTruthy();
  });
});
