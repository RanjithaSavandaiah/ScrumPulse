import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { TechTalkModalComponent } from './tech-talk-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('TechTalkModalComponent', () => {
  let component: TechTalkModalComponent;
  let fixture: ComponentFixture<TechTalkModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TechTalkModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TechTalkModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have duration options', () => {
    expect(component).toBeTruthy();
    expect(component.durationMinutes).toBe(30);
    expect(component.durations.length).toBe(4);
  });

  it('should emit save on valid submission', () => {
    spyOn(component.save, 'emit');

    component.topic = 'Angular 22 Signals & Zoneless';
    component.presenterId = 'm-1';
    component.durationMinutes = 45;

    component.onSubmit();
    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      topic: 'Angular 22 Signals & Zoneless',
      presenterId: 'm-1',
      durationMinutes: 45
    }));
  });

  it('should not emit save when topic or presenterId is blank', () => {
    spyOn(component.save, 'emit');

    component.topic = '   ';
    component.presenterId = '';
    component.onSubmit();

    expect(component.save.emit).not.toHaveBeenCalled();
  });
});
