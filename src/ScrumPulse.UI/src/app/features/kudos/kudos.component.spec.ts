import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { KudosComponent } from './kudos.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('KudosComponent', () => {
  let component: KudosComponent;
  let fixture: ComponentFixture<KudosComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KudosComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(KudosComponent);
    component = fixture.componentInstance;
  });

  it('should map badge types to icons and labels', () => {
    expect(component.getBadgeLabel(0)).toBe('Problem Solver');
    expect(component.getBadgeIcon(0)).toBe('rocket');

    expect(component.getBadgeLabel(1)).toBe('Team Player');
    expect(component.getBadgeIcon(1)).toBe('users');
  });
});
