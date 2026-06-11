import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AstronomyService } from './services/astronomy.service';
import { formatApodDate } from './utils/format-date';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  private readonly astronomy = inject(AstronomyService);

  readonly selectedDate = this.astronomy.selectedDate;
  readonly formattedDate = computed(() => formatApodDate(this.selectedDate()));

  private readonly index = computed(() =>
    this.astronomy.availableDates.indexOf(this.selectedDate())
  );
  readonly canPrev = computed(() => this.index() > 0);
  readonly canNext = computed(() => this.index() < this.astronomy.availableDates.length - 1);

  /** Step to the chronologically previous (older) archive date. */
  prev(): void {
    if (this.canPrev()) {
      this.astronomy.selectDate(this.astronomy.availableDates[this.index() - 1]);
    }
  }

  /** Step to the chronologically next (newer) archive date. */
  next(): void {
    if (this.canNext()) {
      this.astronomy.selectDate(this.astronomy.availableDates[this.index() + 1]);
    }
  }
}
