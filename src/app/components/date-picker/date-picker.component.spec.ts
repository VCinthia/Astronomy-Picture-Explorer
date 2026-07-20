import { ComponentFixture, TestBed } from '@angular/core/testing';

import { APOD_FIRST_DATE, utcToday } from '../../services/astronomy.service';
import { DatePickerComponent } from './date-picker.component';

describe('DatePickerComponent', () => {
  let fixture: ComponentFixture<DatePickerComponent>;
  let input: HTMLInputElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DatePickerComponent] }).compileComponents();
    fixture = TestBed.createComponent(DatePickerComponent);
    fixture.componentRef.setInput('selected', '2026-05-24');
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input[type="date"]') as HTMLInputElement;
  });

  it('uses an accessible native APOD date input with UTC calendar bounds', () => {
    const label = fixture.nativeElement.querySelector('label') as HTMLLabelElement;

    expect(input.value).toBe('2026-05-24');
    expect(input.min).toBe(APOD_FIRST_DATE);
    expect(input.max).toBe(utcToday());
    expect(label.htmlFor).toBe(input.id);
    expect(label.textContent).toContain('Pick a date');
  });

  it('emits a valid date without relying on a preloaded date list', () => {
    let emitted: string | undefined;
    fixture.componentInstance.dateSelected.subscribe((date) => (emitted = date));

    input.value = '2026-05-22';
    input.dispatchEvent(new Event('change'));

    expect(emitted).toBe('2026-05-22');
    expect(fixture.nativeElement.querySelector('[role="listbox"]')).toBeNull();
  });

  it('does not emit malformed or out-of-range values', () => {
    const emitted: string[] = [];
    fixture.componentInstance.dateSelected.subscribe((date) => emitted.push(date));

    fixture.componentInstance.onDateChange({ target: { value: 'not-a-date' } } as unknown as Event);
    fixture.componentInstance.onDateChange({ target: { value: '1995-06-15' } } as unknown as Event);

    expect(emitted).toEqual([]);
  });
});
