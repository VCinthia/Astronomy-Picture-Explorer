import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';

import { ColorPaletteComponent } from '../color-palette/color-palette.component';
import type { ApodEntry } from '../../models/apod.model';
import { formatApodDate } from '../../utils/format-date';

/**
 * Renders a single APOD entry: the media (image or video), its dominant-color
 * palette, and the title/date/description metadata. Layout and tokens mirror the
 * Figma "Desktop/Mobile Home" and "Video State" frames. A spinner covers the
 * media until it loads, since NASA images can be large and slow.
 */
@Component({
  selector: 'app-picture-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ColorPaletteComponent],
  templateUrl: './picture-card.component.html',
  styleUrl: './picture-card.component.css'
})
export class PictureCardComponent {
  readonly entry = input.required<ApodEntry>();

  readonly isVideo = computed(() => this.entry().media_type === 'video');
  readonly formattedDate = computed(() => formatApodDate(this.entry().date));

  /** False while the current media is still loading (drives the spinner). */
  readonly mediaLoaded = signal(false);

  constructor() {
    // Reset the loading state whenever the displayed entry changes.
    effect(() => {
      this.entry();
      this.mediaLoaded.set(false);
    });
  }

  onMediaSettled(): void {
    this.mediaLoaded.set(true);
  }
}
