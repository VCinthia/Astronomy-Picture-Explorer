import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { APOD_FIRST_DATE, isApodDate, utcToday } from '../../services/astronomy.service';

/** Native date control for every date supported by APOD, not a preloaded archive subset. */
@Component({
  selector: 'app-date-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex w-full flex-col gap-2">
      <label [for]="inputId" class="text-micro font-medium uppercase tracking-widest text-content-secondary">
        Pick a date
      </label>
      <input
        [id]="inputId"
        type="date"
        [value]="selected()"
        [min]="minDate"
        [max]="maxDate"
        (change)="onDateChange($event)"
        class="h-11 w-full rounded-button border border-space-border bg-space-surface-hi px-4 text-meta text-content-primary transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      />
    </div>
  `
})
export class DatePickerComponent {
  readonly selected = input.required<string>();
  readonly dateSelected = output<string>();

  readonly inputId = 'apod-date-picker';
  readonly minDate = APOD_FIRST_DATE;
  readonly maxDate = utcToday();

  onDateChange(event: Event): void {
    const date = (event.target as HTMLInputElement).value;
    if (isApodDate(date)) {
      this.dateSelected.emit(date);
    }
  }
}
