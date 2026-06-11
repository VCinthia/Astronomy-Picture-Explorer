import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ColorPaletteComponent } from '../color-palette/color-palette.component';
import type { ApodEntry } from '../../models/apod.model';
import { formatApodDate } from '../../utils/format-date';

/**
 * Renders a single APOD entry: the media (image or video), its dominant-color
 * palette, and the title/date/description metadata. Layout and tokens mirror the
 * Figma "Desktop/Mobile Home" and "Video State" frames.
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
}
