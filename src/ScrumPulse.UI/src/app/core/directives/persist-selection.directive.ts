import {
  AfterViewChecked,
  AfterViewInit,
  DestroyRef,
  Directive,
  ElementRef,
  PLATFORM_ID,
  effect,
  inject,
  input
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NgControl } from '@angular/forms';

/**
 * Remembers a dropdown's selected value across page reloads.
 *
 * Every filter and selector in the application reset on refresh, which meant a Scrum Master
 * who picked a squad, a sprint and a member, then reloaded, had to pick all three again. The
 * selection is a user preference rather than application state, so it belongs in local
 * storage rather than in the store.
 *
 * Applied with a single attribute so a dropdown opts in without bespoke wiring:
 *
 *   <select [(ngModel)]="selectedSprintId" persistSelection="workitems.sprint">
 *   <select (change)="onFilter($event)" persistSelection="blockers.statusFilter">
 *
 * Works with or without ngModel. Where a form control is present the value is written through
 * it so Angular's own state stays in step; otherwise the value is set on the element and a
 * change event dispatched, which is what a plain (change) handler is listening for.
 *
 * Keys are namespaced by caller (for example "workitems.sprint") so two screens with a
 * "status" filter do not overwrite each other.
 */
@Directive({
  selector: 'select[persistSelection]',
  standalone: true
})
export class PersistSelectionDirective implements AfterViewInit, AfterViewChecked {
  /** Storage key for this dropdown. Namespace it by screen, e.g. "capacity.member". */
  readonly persistSelection = input.required<string>();

  /** Prefix keeping these preferences distinct from other local storage entries. */
  private static readonly KeyPrefix = 'scrumpulse.selection.';

  private readonly selectElement = inject<ElementRef<HTMLSelectElement>>(ElementRef).nativeElement;
  private readonly formControl = inject(NgControl, { optional: true, self: true });
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);

  /** Stops the observer re-applying a stored value after the user has chosen something. */
  private hasRestored = false;
  private optionObserver?: MutationObserver;

  constructor() {
    // Persist on every change, including programmatic ones made through the form control.
    this.selectElement.addEventListener('change', this.onSelectionChanged);
    this.destroyRef.onDestroy(() => {
      this.selectElement.removeEventListener('change', this.onSelectionChanged);
    });

    // A form control may receive its value after the element is created, so the stored value
    // is re-applied whenever the control's value settles to empty.
    effect(() => {
      this.persistSelection();
      this.hasRestored = false;
    });
  }

  ngAfterViewInit(): void {
    if (!this.isBrowser) {
      return;
    }

    const storedValue = this.readStoredValue();

    if (storedValue === null) {
      return;
    }

    if (this.tryApply(storedValue)) {
      return;
    }

    // Options are usually populated from an HTTP response that has not arrived yet, so the
    // stored value matches nothing on the first pass. Watching for options to appear is more
    // reliable than guessing a delay, and the observer disconnects as soon as it succeeds so
    // it cannot keep overriding a choice the user makes later.
    this.optionObserver = new MutationObserver(() => {
      if (this.tryApply(storedValue)) {
        this.optionObserver?.disconnect();
      }
    });

    this.optionObserver.observe(this.selectElement, { childList: true, subtree: true });

    this.destroyRef.onDestroy(() => this.optionObserver?.disconnect());
  }

  ngAfterViewChecked(): void {
    if (!this.isBrowser || this.hasRestored) {
      return;
    }

    const storedValue = this.readStoredValue();
    if (storedValue !== null && this.tryApply(storedValue)) {
      this.optionObserver?.disconnect();
    }
  }

  /**
   * Applies the stored value if the dropdown now contains a matching option.
   *
   * Returns false when it does not, so the caller keeps waiting rather than selecting
   * nothing: assigning an absent value to a select silently blanks it, which would look like
   * the bug this directive exists to fix.
   */
  private tryApply(storedValue: string): boolean {
    if (this.hasRestored) {
      return true;
    }

    const hasMatchingOption = Array.from(this.selectElement.options).some(
      (option) => option.value === storedValue
    );

    if (!hasMatchingOption) {
      return false;
    }

    this.hasRestored = true;

    this.selectElement.value = storedValue;
    if (this.formControl?.control) {
      this.formControl.control.setValue(storedValue);
    }

    // Dispatched either way so components listening on (change) react, and so any derived
    // state recomputes exactly as it would after a real selection.
    this.selectElement.dispatchEvent(new Event('change', { bubbles: true }));

    return true;
  }

  private readonly onSelectionChanged = (): void => {
    if (!this.isBrowser) {
      return;
    }

    // A restore dispatches its own change event; writing it back is harmless but pointless,
    // and marking it restored here stops the observer firing again.
    this.hasRestored = true;

    try {
      if (this.selectElement.value !== '__join__') {
        localStorage.setItem(this.storageKey(), this.selectElement.value);
      }
    } catch {
      // Private browsing and some enterprise policies make writes throw. The dropdown still
      // works for this session; only remembering it is lost, which is not worth interrupting
      // the user over.
    }
  };

  private readStoredValue(): string | null {
    try {
      return localStorage.getItem(this.storageKey());
    } catch {
      return null;
    }
  }

  private storageKey(): string {
    return `${PersistSelectionDirective.KeyPrefix}${this.persistSelection()}`;
  }
}
