import { Injectable, computed, signal } from '@angular/core';

import apodData from '../../assets/mock/apod.json';
import type { ApodEntry, ApodMock } from '../models/apod.model';

/**
 * The mock archive is imported (bundled) so lookups are synchronous and O(1).
 * In P3 this single source is swapped for an HTTP-backed implementation without
 * changing the `ApodEntry` contract consumed by the components.
 */
const APOD_DATA = apodData as unknown as ApodMock;

@Injectable({ providedIn: 'root' })
export class AstronomyService {
  private readonly data: ApodMock = APOD_DATA;

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

  /** Direct O(1) lookup against the archive object. */
  getByDate(date: string): ApodEntry | undefined {
    return this.data[date];
  }

  /** True when the archive contains an entry for the given date. */
  hasDate(date: string): boolean {
    return date in this.data;
  }

  /** Update the selected date that drives `currentPicture`. */
  selectDate(date: string): void {
    this.selectedDate.set(date);
  }

  private resolveInitialDate(): string {
    const today = new Date().toISOString().slice(0, 10);
    return this.hasDate(today) ? today : this.latestDate;
  }
}
