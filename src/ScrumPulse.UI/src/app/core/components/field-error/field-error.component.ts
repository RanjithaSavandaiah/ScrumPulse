import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgModel } from '@angular/forms';

/**
 * Shows the first validation message for one field, directly beneath it.
 *
 * Every modal already displays a single banner at the top saying something is wrong. That
 * tells the user there is a problem but not where, which on a form with eight fields means
 * hunting. This sits under the control it describes.
 *
 * Nothing renders until the control has been touched or the form submitted, so a blank form
 * does not greet the user in red before they have typed anything.
 *
 * Usage:
 *   <input name="title" [(ngModel)]="item.title" #title="ngModel" required maxlength="120" />
 *   <app-field-error [control]="title" label="Title" />
 */
@Component({
  selector: 'app-field-error',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message(); as text) {
      <p class="field-error" role="alert">{{ text }}</p>
    }
  `
})
export class FieldErrorComponent {
  /** The ngModel directive for the field, obtained with #ref="ngModel". */
  readonly control = input.required<NgModel>();

  /** Field name used in the message, so it reads as a sentence rather than a code. */
  readonly label = input('This field');

  /** Shows messages before the user has interacted. Set when a submit is attempted. */
  readonly showImmediately = input(false);

  protected message(): string | null {
    const control = this.control();

    if (!control.invalid || !(control.touched || control.dirty || this.showImmediately())) {
      return null;
    }

    const errors = control.errors;

    if (!errors) {
      return null;
    }

    // Ordered deliberately: "required" first, because on an empty field every other rule
    // also fails and reporting a length constraint on a blank input is confusing.
    if (errors['required']) {
      return `${this.label()} is required.`;
    }

    if (errors['minlength']) {
      return `${this.label()} must be at least ${errors['minlength'].requiredLength} characters.`;
    }

    if (errors['maxlength']) {
      return `${this.label()} must be ${errors['maxlength'].requiredLength} characters or fewer.`;
    }

    if (errors['email']) {
      return 'Enter a valid email address.';
    }

    if (errors['min']) {
      return `${this.label()} must be at least ${errors['min'].min}.`;
    }

    if (errors['max']) {
      return `${this.label()} must be no more than ${errors['max'].max}.`;
    }

    if (errors['pattern']) {
      return `${this.label()} is not in the expected format.`;
    }

    // Unknown validator. Naming the field is still more use than saying nothing, and the
    // generic wording is a signal to add a specific case above.
    return `${this.label()} is not valid.`;
  }
}
