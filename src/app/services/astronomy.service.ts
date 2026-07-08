import { Injectable, computed, effect, signal } from '@angular/core';

import apodData from '../../assets/mock/apod.json';
import type { ApodEntry, ApodMock } from '../models/apod.model';

/**
 * The mock archive is imported (bundled) so lookups are synchronous and O(1).
 * In P3 this single source is swapped for an HTTP-backed implementation without
 * changing the `ApodEntry` contract consumed by the components.
 */
const APOD_DATA = apodData as unknown as ApodMock;
const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';

@Injectable({ providedIn: 'root' })
export class AstronomyService {
  private readonly data: ApodMock = APOD_DATA;

  private readonly favoriteDates = signal<string[]>(this.readStoredFavorites());

  /** Favorite archive dates, persisted locally for the current browser. */
  readonly favorites = this.favoriteDates.asReadonly();

  /** Current keyword used to filter the bundled archive. */
  readonly searchQuery = signal('');

  /** Entries whose title or explanation contain the normalized search query. */
  readonly searchResults = computed<ApodEntry[]>(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();
    if (!query) {
      return [];
    }

    return this.availableDates
      .map((date) => this.data[date])
      .filter(({ title, explanation }) => {
        return (
          title.toLocaleLowerCase().includes(query) ||
          explanation.toLocaleLowerCase().includes(query)
        );
      });
  });

  /** Dates present in the archive, sorted oldest to newest (`YYYY-MM-DD`). */
  readonly availableDates: readonly string[] = Object.keys(this.data).sort();

  /** Most recent date in the archive; used as the default when today is absent. */
  readonly latestDate = this.availableDates[this.availableDates.length - 1];

  /** The "home" date: today's entry if present, otherwise the latest. */
  readonly defaultDate = this.resolveInitialDate();

  /** Currently selected archive date. Defaults to today, or the latest entry. */
  readonly selectedDate = signal<string>(this.defaultDate);

  /** Entry for the selected date, or `undefined` when the date has no entry. */
  readonly currentPicture = computed<ApodEntry | undefined>(() =>
    this.getByDate(this.selectedDate())
  );

  /** Reserved for the async P3 backend; always settled (`false`) for the mock. */
  readonly loading = signal(false);

  /** Reserved for the async P3 backend; `null` while the mock has no errors. */
  readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.persistFavorites(this.favoriteDates());
    });
  }

  /** Direct O(1) lookup against the archive object. */
  getByDate(date: string): ApodEntry | undefined {
    return this.hasDate(date) ? this.data[date] : undefined;
  }

  /** True when the archive contains an entry for the given date. */
  hasDate(date: string): boolean {
    return Object.prototype.hasOwnProperty.call(this.data, date);
  }

  /** Update the selected date that drives `currentPicture`. */
  selectDate(date: string): void {
    this.selectedDate.set(date);
  }

  /** Add or remove a valid archive date from the persisted favorites. */
  toggleFavorite(date: string): void {
    if (!this.hasDate(date)) {
      return;
    }

    this.favoriteDates.update((dates) =>
      dates.includes(date) ? dates.filter((favorite) => favorite !== date) : [...dates, date]
    );
  }

  /** True when the provided archive date is currently a favorite. */
  isFavorite(date: string): boolean {
    return this.favoriteDates().includes(date);
  }

  private resolveInitialDate(): string {
    const today = new Date().toISOString().slice(0, 10);
    return this.hasDate(today) ? today : this.latestDate;
  }

  private readStoredFavorites(): string[] {
    try {
      if (typeof localStorage === 'undefined') {
        return [];
      }

      const storedValue = localStorage.getItem(FAVORITES_STORAGE_KEY);
      if (storedValue === null) {
        return [];
      }

      const parsedValue: unknown = JSON.parse(storedValue);
      if (!Array.isArray(parsedValue) || !parsedValue.every((date) => typeof date === 'string')) {
        return [];
      }

      return [...new Set(parsedValue)].filter((date) => this.hasDate(date));
    } catch {
      return [];
    }
  }

  private persistFavorites(dates: readonly string[]): void {
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(FAVORITES_STORAGE_KEY, JSON.stringify(dates));
      }
    } catch {
      // Storage can be unavailable (privacy mode, quota, or a denied origin).
      // Favorites remain usable in memory for the lifetime of the service.
    }
  }
}
