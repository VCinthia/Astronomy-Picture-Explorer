import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';

import { AstronomyService } from '../../services/astronomy.service';
import { PictureCardComponent } from '../../components/picture-card/picture-card.component';

/**
 * Landing view: shows the picture of the day (today's entry, or the most recent
 * one in the archive when today is absent from the mock).
 */
@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PictureCardComponent],
  template: `
    @if (picture(); as entry) {
      <app-picture-card [entry]="entry" />
    } @else {
      <p class="text-body text-content-secondary">No picture available for this date.</p>
    }
  `
})
export class HomeComponent implements OnInit {
  private readonly astronomy = inject(AstronomyService);

  readonly picture = this.astronomy.currentPicture;

  ngOnInit(): void {
    // Home always lands on the picture of the day, regardless of prior selection.
    this.astronomy.selectDate(this.astronomy.defaultDate);
  }
}
