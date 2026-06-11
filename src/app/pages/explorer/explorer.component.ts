import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { AstronomyService } from '../../services/astronomy.service';
import { DatePickerComponent } from '../../components/date-picker/date-picker.component';
import { PictureCardComponent } from '../../components/picture-card/picture-card.component';

/**
 * Archive explorer: pick any date present in the mock and see its entry. The
 * date picker and the card are both driven by the service's `selectedDate`.
 */
@Component({
  selector: 'app-explorer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePickerComponent, PictureCardComponent],
  template: `
    <div class="flex flex-col gap-8">
      <app-date-picker
        [dates]="dates"
        [selected]="selectedDate()"
        (dateSelected)="onSelect($event)"
      />
      @if (picture(); as entry) {
        <app-picture-card [entry]="entry" />
      } @else {
        <p class="text-body text-content-secondary">No picture available for this date.</p>
      }
    </div>
  `
})
export class ExplorerComponent {
  private readonly astronomy = inject(AstronomyService);

  /** Available dates, newest first, for the picker. */
  readonly dates = [...this.astronomy.availableDates].reverse();
  readonly selectedDate = this.astronomy.selectedDate;
  readonly picture = this.astronomy.currentPicture;

  onSelect(date: string): void {
    this.astronomy.selectDate(date);
  }
}
