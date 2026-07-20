import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { Router } from '@angular/router';

import { ColorPaletteComponent } from '../color-palette/color-palette.component';
import type { ApodEntry } from '../../models/apod.model';
import { AuthService } from '../../services/auth.service';
import { FavoritesService } from '../../services/favorites.service';
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
  private readonly favorites = inject(FavoritesService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly entry = input.required<ApodEntry>();

  readonly isVideo = computed(() => this.entry().media_type === 'video');
  readonly isFavorite = computed(() => this.favorites.isFavorite(this.entry().date));
  readonly favoritePending = computed(() => this.favorites.isPending(this.entry().date));
  readonly canSaveFavorites = this.auth.isAuthenticated;
  readonly formattedDate = computed(() => formatApodDate(this.entry().date));

  /** False while the current media is still loading (drives the spinner). */
  readonly mediaLoaded = signal(false);

  constructor() {
    // Reset the loading state whenever the displayed entry changes.
    effect(() => {
      const entry = this.entry();
      // A video without a provider thumbnail has no media load event to settle the
      // spinner. It is immediately actionable through its external video link.
      this.mediaLoaded.set(entry.media_type === 'video' && entry.thumbnail_url === null);
    });
  }

  onMediaSettled(): void {
    this.mediaLoaded.set(true);
  }

  toggleFavorite(): void {
    this.favorites.toggle(this.entry(), this.router.url);
  }
}
