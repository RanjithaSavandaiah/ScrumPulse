import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { TechHubComponent } from './tech-hub.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('TechHubComponent', () => {
  let component: TechHubComponent;
  let fixture: ComponentFixture<TechHubComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TechHubComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(TechHubComponent);
    component = fixture.componentInstance;
  });

  it('should create successfully', () => {
    expect(component).toBeTruthy();
  });
});
