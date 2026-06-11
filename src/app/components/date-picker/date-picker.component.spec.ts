import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DatePickerComponent } from './date-picker.component';

const DATES = ['2026-05-22', '2026-05-24', '2026-06-09'];

describe('DatePickerComponent', () => {
  let fixture: ComponentFixture<DatePickerComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DatePickerComponent] }).compileComponents();

    fixture = TestBed.createComponent(DatePickerComponent);
    fixture.componentRef.setInput('dates', DATES);
    fixture.componentRef.setInput('selected', '2026-05-24');
    fixture.detectChanges();
    el = fixture.nativeElement as HTMLElement;
  });

  function trigger(): HTMLButtonElement {
    return el.querySelector('button[aria-controls="date-picker-listbox"]') as HTMLButtonElement;
  }

  function expand(): void {
    trigger().click();
    fixture.detectChanges();
  }

  function listbox(): HTMLElement {
    return el.querySelector('[role="listbox"]') as HTMLElement;
  }

  it('is collapsed by default and shows the selected date on the trigger', () => {
    expect(listbox()).toBeNull();
    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    expect(trigger().textContent).toContain('May 24, 2026');
  });

  it('renders one option per available date when expanded', () => {
    expand();
    const options = el.querySelectorAll('[role="option"]');

    expect(options.length).toBe(DATES.length);
    expect(options[0].textContent).toContain('May 22, 2026');
  });

  it('marks the selected date with aria-selected', () => {
    expand();
    expect(el.querySelector('[aria-selected="true"]')?.id).toBe('date-option-2026-05-24');
  });

  it('points aria-activedescendant at the selected option initially', () => {
    expand();
    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-05-24');
  });

  it('emits the date and collapses when an option is clicked', () => {
    let emitted: string | undefined;
    fixture.componentInstance.dateSelected.subscribe((d) => (emitted = d));

    expand();
    (el.querySelector('#date-option-2026-06-09') as HTMLElement).click();
    fixture.detectChanges();

    expect(emitted).toBe('2026-06-09');
    expect(listbox()).toBeNull();
  });

  it('moves the active option with ArrowDown and selects with Enter', () => {
    let emitted: string | undefined;
    fixture.componentInstance.dateSelected.subscribe((d) => (emitted = d));

    expand();
    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    fixture.detectChanges();
    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-06-09');

    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(emitted).toBe('2026-06-09');
  });

  it('jumps to the first option with Home', () => {
    expand();
    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    fixture.detectChanges();

    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-05-22');
  });
});
