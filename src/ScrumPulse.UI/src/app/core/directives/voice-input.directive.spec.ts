import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { VoiceInputDirective } from './voice-input.directive';
import { DictationTarget, VoiceDictationService } from '../services/voice-dictation.service';

/**
 * Covers the attribute that marks a field as a destination for dictated speech.
 *
 * The bug this exists to prevent is silent data loss. Writing `element.value` fills the box on
 * screen but leaves Angular's model untouched, so the user sees their dictated text, clicks
 * Save, and the component submits an empty string. Every test below that touches a form
 * control is guarding that.
 */
describe('VoiceInputDirective', () => {
  /** Records target registration without running real audio capture. */
  class FakeDictationService {
    target: DictationTarget | null = null;
    clearAttempts: DictationTarget[] = [];

    setTarget(target: DictationTarget): void {
      this.target = target;
    }

    clearTarget(target: DictationTarget): void {
      this.clearAttempts.push(target);

      if (this.target === target) {
        this.target = null;
      }
    }
  }

  @Component({
    standalone: true,
    imports: [VoiceInputDirective, ReactiveFormsModule],
    template: `
      @if (isFieldPresent()) {
        <textarea [formControl]="notes" [voiceInput]="fieldLabel()"></textarea>
      }
      <input #plain type="text" [voiceInput]="'Plain field'" />
    `
  })
  class HostComponent {
    readonly notes = new FormControl('');
    readonly fieldLabel = signal('Blocker description');
    readonly isFieldPresent = signal(true);
  }

  let dictation: FakeDictationService;
  let fixture: ComponentFixture<HostComponent>;

  function boundField(): HTMLTextAreaElement {
    return fixture.nativeElement.querySelector('textarea');
  }

  function unboundField(): HTMLInputElement {
    return fixture.nativeElement.querySelector('input');
  }

  beforeEach(() => {
    dictation = new FakeDictationService();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: VoiceDictationService, useValue: dictation }]
    });

    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('claims the dictation target on focus', () => {
    boundField().dispatchEvent(new Event('focus'));

    expect(dictation.target).not.toBeNull();
  });

  it('reports the field name given in the attribute', () => {
    boundField().dispatchEvent(new Event('focus'));

    expect(dictation.target?.label).toBe('Blocker description');
  });

  it('follows a label that changes after binding', () => {
    // The label reads the input signal rather than copying it, so a field whose name depends
    // on state - "Update for Priya" - stays accurate in the capture bar.
    boundField().dispatchEvent(new Event('focus'));
    fixture.componentInstance.fieldLabel.set('Update for Priya');
    fixture.detectChanges();

    expect(dictation.target?.label).toBe('Update for Priya');
  });

  it('does not release the target on blur', () => {
    // Clicking the capture bar's microphone moves focus off the field. Releasing here would
    // drop the target at the exact moment dictation starts.
    boundField().dispatchEvent(new Event('focus'));

    boundField().dispatchEvent(new Event('blur'));

    expect(dictation.target).not.toBeNull();
  });

  it('hands the target to whichever field was focused last', () => {
    boundField().dispatchEvent(new Event('focus'));
    unboundField().dispatchEvent(new Event('focus'));

    expect(dictation.target?.label).toBe('Plain field');
  });

  it('writes through the form control, not just the element', () => {
    // The core guarantee. A field that only sets `element.value` submits an empty string.
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Finished the migration');

    expect(fixture.componentInstance.notes.value).toBe('Finished the migration');
  });

  it('keeps text the user had already typed', () => {
    fixture.componentInstance.notes.setValue('Context:');
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('finished the migration');

    expect(fixture.componentInstance.notes.value).toBe('Context: finished the migration');
  });

  it('does not double the space when text already ends in one', () => {
    fixture.componentInstance.notes.setValue('Context: ');
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('finished');

    expect(fixture.componentInstance.notes.value).toBe('Context: finished');
  });

  it('adds no leading space to an empty field', () => {
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Finished');

    expect(fixture.componentInstance.notes.value).toBe('Finished');
  });

  it('accumulates successive slices, as the typewriter delivers them', () => {
    // Text arrives a few characters at a time, so each append builds on the last.
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Fin');
    dictation.target?.append('ished');

    expect(fixture.componentInstance.notes.value).toBe('Finished');
  });

  it('leaves the caret at the end so the user can carry on typing', () => {
    boundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Finished the migration');

    expect(boundField().selectionStart).toBe('Finished the migration'.length);
  });

  it('writes into a field that has no form control', () => {
    unboundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Plain text');

    expect(unboundField().value).toBe('Plain text');
  });

  it('raises an input event so listeners react as they would to typing', () => {
    // Components binding with (input) or a template-driven model only learn about the change
    // through this event.
    let observed = '';
    unboundField().addEventListener('input', (event) => {
      observed = (event.target as HTMLInputElement).value;
    });
    unboundField().dispatchEvent(new Event('focus'));

    dictation.target?.append('Plain text');

    expect(observed).toBe('Plain text');
  });

  it('releases the target when the field is destroyed', () => {
    // A modal closed mid-dictation would otherwise leave the capture bar pointing at a field
    // that no longer exists, and the text would vanish.
    boundField().dispatchEvent(new Event('focus'));

    fixture.componentInstance.isFieldPresent.set(false);
    fixture.detectChanges();

    expect(dictation.target).toBeNull();
  });

  it('does not disturb another field when destroyed while inactive', () => {
    boundField().dispatchEvent(new Event('focus'));
    unboundField().dispatchEvent(new Event('focus'));

    fixture.componentInstance.isFieldPresent.set(false);
    fixture.detectChanges();

    expect(dictation.target?.label).toBe('Plain field');
  });
});
