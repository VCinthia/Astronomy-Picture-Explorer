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

  function listbox(): HTMLElement {
    return el.querySelector('[role="listbox"]') as HTMLElement;
  }

  it('renders one option per available date', () => {
    const options = el.querySelectorAll('[role="option"]');

    expect(options.length).toBe(DATES.length);
    expect(options[0].textContent).toContain('May 22, 2026');
  });

  it('marks the selected date with aria-selected', () => {
    const selected = el.querySelector('[aria-selected="true"]');

    expect(selected?.id).toBe('date-option-2026-05-24');
  });

  it('points aria-activedescendant at the selected option initially', () => {
    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-05-24');
  });

  it('emits the date when an option is clicked', () => {
    let emitted: string | undefined;
    fixture.componentInstance.dateSelected.subscribe((d) => (emitted = d));

    (el.querySelector('#date-option-2026-06-09') as HTMLElement).click();

    expect(emitted).toBe('2026-06-09');
  });

  it('moves the active option with ArrowDown and selects with Enter', () => {
    let emitted: string | undefined;
    fixture.componentInstance.dateSelected.subscribe((d) => (emitted = d));

    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    fixture.detectChanges();
    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-06-09');

    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(emitted).toBe('2026-06-09');
  });

  it('jumps to the first option with Home', () => {
    listbox().dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    fixture.detectChanges();

    expect(listbox().getAttribute('aria-activedescendant')).toBe('date-option-2026-05-22');
  });
});
