import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { SmPinModalComponent } from './sm-pin-modal.component';
import { ScrumStateService } from '../../services/scrum-state.service';
import { appReducers } from '../../state';

describe('SmPinModalComponent', () => {
  let component: SmPinModalComponent;
  let fixture: ComponentFixture<SmPinModalComponent>;
  let stateService: ScrumStateService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SmPinModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SmPinModalComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
    expect(component.pin).toBe('');
  });

  it('should append digits up to 4 digits and support backspace and clear', () => {
    component.appendDigit(1);
    component.appendDigit('2');
    component.appendDigit(3);
    expect(component.pin).toBe('123');

    component.backspace();
    expect(component.pin).toBe('12');

    component.clear();
    expect(component.pin).toBe('');
  });

  it('should emit authenticated event on successful PIN verification', () => {
    spyOn(stateService, 'verifyAndUnlockSm').and.returnValue(of(true));
    spyOn(component.authenticated, 'emit');

    component.pin = '1234';
    component.submitPin();

    expect(stateService.verifyAndUnlockSm).toHaveBeenCalledWith('1234');
    expect(component.authenticated.emit).toHaveBeenCalled();
  });

  it('should show error message and clear pin on incorrect PIN', () => {
    spyOn(stateService, 'verifyAndUnlockSm').and.returnValue(of(false));

    component.pin = '9999';
    component.submitPin();

    expect(component.errorMessage()).toContain('Incorrect Security PIN');
    expect(component.pin).toBe('');
  });

  it('should emit cancelled event on cancel or Escape key', () => {
    spyOn(component.cancelled, 'emit');

    component.onCancel();
    expect(component.cancelled.emit).toHaveBeenCalled();

    const escapeEvent = new KeyboardEvent('keydown', { key: 'Escape' });
    component.handleKeyDown(escapeEvent);
    expect(component.cancelled.emit).toHaveBeenCalledTimes(2);
  });
});
