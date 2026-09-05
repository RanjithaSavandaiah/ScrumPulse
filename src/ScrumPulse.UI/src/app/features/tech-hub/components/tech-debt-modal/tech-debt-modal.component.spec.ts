import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { TechDebtModalComponent } from './tech-debt-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('TechDebtModalComponent', () => {
  let component: TechDebtModalComponent;
  let fixture: ComponentFixture<TechDebtModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TechDebtModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TechDebtModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create with default estimatedHours and status', () => {
    expect(component).toBeTruthy();
    expect(component.estimatedHours).toBe(8);
    expect(component.status).toBe('Identified');
  });

  it('should emit save on valid submission', () => {
    spyOn(component.save, 'emit');

    component.title = 'Migrate to NgRx SignalStore';
    component.severity = 'High';
    component.onSubmit();

    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'Migrate to NgRx SignalStore',
      severity: 'High'
    }));
  });

  it('should not emit save when title is empty', () => {
    spyOn(component.save, 'emit');

    component.title = '   ';
    component.onSubmit();

    expect(component.save.emit).not.toHaveBeenCalled();
  });
});
