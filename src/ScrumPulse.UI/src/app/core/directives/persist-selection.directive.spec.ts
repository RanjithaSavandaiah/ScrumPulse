import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { PersistSelectionDirective } from './persist-selection.directive';

/** Host whose options arrive after first render, as they do from an HTTP response. */
@Component({
  standalone: true,
  imports: [FormsModule, PersistSelectionDirective],
  template: `
    <select [(ngModel)]="selected" name="sprint" persistSelection="spec.sprint">
      <option value="ALL">All</option>
      @for (option of options(); track option) {
        <option [value]="option">{{ option }}</option>
      }
    </select>
  `
})
class HostComponent {
  selected = 'ALL';
  readonly options = signal<string[]>([]);
}

describe('PersistSelectionDirective', () => {
  const storageKey = 'scrumpulse.selection.spec.sprint';
  let fixture: ComponentFixture<HostComponent>;

  function selectElement(): HTMLSelectElement {
    return fixture.nativeElement.querySelector('select');
  }

  async function createHost(): Promise<void> {
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  beforeEach(() => {
    localStorage.removeItem(storageKey);
    TestBed.configureTestingModule({ imports: [HostComponent] });
  });

  afterEach(() => localStorage.removeItem(storageKey));

  it('stores the value when the user picks an option', async () => {
    await createHost();
    fixture.componentInstance.options.set(['sprint-14']);
    fixture.detectChanges();

    const select = selectElement();
    select.value = 'sprint-14';
    select.dispatchEvent(new Event('change'));

    expect(localStorage.getItem(storageKey)).toBe('sprint-14');
  });

  it('restores the stored value on a later load', async () => {
    localStorage.setItem(storageKey, 'sprint-14');

    await createHost();
    fixture.componentInstance.options.set(['sprint-14']);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(selectElement().value).toBe('sprint-14');
  });

  it('waits for options that load after the first render', async () => {
    // The case that makes this directive non-trivial: on first paint the select holds only
    // "All", so the stored value matches nothing and a naive implementation gives up.
    localStorage.setItem(storageKey, 'sprint-22');

    await createHost();
    expect(selectElement().value).not.toBe('sprint-22');

    fixture.componentInstance.options.set(['sprint-21', 'sprint-22']);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(selectElement().value).toBe('sprint-22');
  });

  it('leaves the dropdown alone when the stored value no longer exists', async () => {
    // A deleted sprint must not blank the control: assigning an absent value to a select
    // clears it, which looks exactly like the bug this directive fixes.
    localStorage.setItem(storageKey, 'sprint-deleted');

    await createHost();
    fixture.componentInstance.options.set(['sprint-30']);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(selectElement().value).toBe('ALL');
  });

  it('writes the restored value through the form control', async () => {
    // Setting only the DOM value would leave ngModel out of step, so the component would
    // filter by one sprint while the dropdown displayed another.
    localStorage.setItem(storageKey, 'sprint-14');

    await createHost();
    fixture.componentInstance.options.set(['sprint-14']);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.selected).toBe('sprint-14');
  });

  it('does not override a choice the user makes after restoring', async () => {
    localStorage.setItem(storageKey, 'sprint-14');

    await createHost();
    fixture.componentInstance.options.set(['sprint-14', 'sprint-15']);
    fixture.detectChanges();
    await fixture.whenStable();

    const select = selectElement();
    select.value = 'sprint-15';
    select.dispatchEvent(new Event('change'));

    // Adding further options must not drag the selection back to the stored value.
    fixture.componentInstance.options.set(['sprint-14', 'sprint-15', 'sprint-16']);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(selectElement().value).toBe('sprint-15');
    expect(localStorage.getItem(storageKey)).toBe('sprint-15');
  });

  it('keeps working when local storage is unavailable', async () => {
    // Private browsing and hardened enterprise policies make these throw.
    spyOn(Storage.prototype, 'getItem').and.throwError('denied');
    spyOn(Storage.prototype, 'setItem').and.throwError('denied');

    await createHost();
    fixture.componentInstance.options.set(['sprint-14']);
    fixture.detectChanges();

    const select = selectElement();

    expect(() => {
      select.value = 'sprint-14';
      select.dispatchEvent(new Event('change'));
    }).not.toThrow();
  });
});
