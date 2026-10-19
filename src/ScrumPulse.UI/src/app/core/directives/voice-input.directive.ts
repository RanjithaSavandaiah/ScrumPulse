import { DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';
import { NgControl } from '@angular/forms';
import { DictationTarget, VoiceDictationService } from '../services/voice-dictation.service';

/**
 * Marks a text field as a destination for dictated speech; focus makes it the active target.
 * Text goes through the form control, since setting `element.value` alone leaves Angular's
 * model untouched and the component would save an empty string.
 */
@Directive({
  selector: '[voiceInput]',
  standalone: true,
  host: {
    '(focus)': 'activate()',
    '(blur)': 'deactivate()'
  }
})
export class VoiceInputDirective implements DictationTarget {
  readonly voiceInput = input.required<string>();

  private readonly element =
    inject<ElementRef<HTMLInputElement | HTMLTextAreaElement>>(ElementRef).nativeElement;

  private readonly formControl = inject(NgControl, { optional: true, self: true });
  private readonly dictation = inject(VoiceDictationService);

  get label(): string {
    return this.voiceInput();
  }

  constructor() {
    // Otherwise the capture bar keeps pointing at a destroyed field.
    inject(DestroyRef).onDestroy(() => this.dictation.clearTarget(this));
  }

  private isFirstChunk = true;

  protected activate(): void {
    this.isFirstChunk = true;
    this.dictation.setTarget(this);
  }

  protected deactivate(): void {
    // Empty by design. Clicking the capture bar's microphone blurs the field, so clearing
    // here would drop the target at the moment dictation starts.
  }

  /** Appends transcribed text after whatever the user already typed. */
  append(text: string): void {
    const existing = this.currentValue();
    const separator = this.isFirstChunk && existing.length > 0 && !existing.endsWith(' ') ? ' ' : '';
    this.isFirstChunk = false;
    const updated = `${existing}${separator}${text}`;

    if (this.formControl?.control) {
      this.formControl.control.setValue(updated);
    } else {
      this.element.value = updated;

      this.element.dispatchEvent(new Event('input', { bubbles: true }));
    }

    // Caret to the end, so typing can continue after dictating.
    this.element.selectionStart = this.element.selectionEnd = updated.length;
  }

  private currentValue(): string {
    return this.formControl?.control?.value ?? this.element.value ?? '';
  }
}
