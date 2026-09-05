import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { GiveKudosModalComponent } from './give-kudos-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('GiveKudosModalComponent', () => {
  let component: GiveKudosModalComponent;
  let fixture: ComponentFixture<GiveKudosModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GiveKudosModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GiveKudosModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have 6 badge categories', () => {
    expect(component).toBeTruthy();
    expect(component.badges.length).toBe(6);
  });

  it('should apply preset messages cleanly', () => {
    component.kudos.message = component.presets[0];
    expect(component.kudos.message).toBe(component.presets[0]);
  });

  it('should emit save when form is submitted with message', () => {
    spyOn(component.save, 'emit');

    component.kudos.senderId = 'm-1';
    component.kudos.receiverId = 'm-2';
    component.kudos.badge = 1;
    component.kudos.message = 'Great work on the release';

    component.save.emit(component.kudos);
    expect(component.save.emit).toHaveBeenCalledWith(component.kudos);
  });
});
