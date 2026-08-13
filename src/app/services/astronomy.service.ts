import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { EMPTY, Subject, catchError, map, of, switchMap } from 'rxjs';

import type { ApodEntry } from '../models/apod.model';

export const APOD_FIRST_DATE = '1995-06-16';
export const APOD_SEARCH_PAGE_SIZE = 12;
export const APOD_PRODUCT_TIME_ZONE = 'America/Argentina/Buenos_Aires';

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const apodCalendarFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: APOD_PRODUCT_TIME_ZONE,
  calendar: 'gregory',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit'
});

export interface ApodRequestError {
  readonly code: string | null;
  readonly message: string;
}

interface PictureRequest {
  readonly endpoint: string;
}

type PictureResult =
  | { readonly entry: ApodEntry }
  | { readonly error: ApodRequestError };

type SearchResult =
  | { readonly entries: readonly ApodEntry[] }
  | { readonly error: ApodRequestError };

/**
 * Browser state for the app-owned APOD HTTP endpoints.
 *
 * APOD content comes exclusively from the backend. Authenticated favorites are
 * owned separately by FavoritesService so catalog state never reaches storage.
 */
@Injectable({ providedIn: 'root' })
export class AstronomyService {
  private readonly pictureRequests = new Subject<PictureRequest>();
  private readonly searchRequests = new Subject<string>();

  /** Date confirmed by the latest APOD response; absent until the first response arrives. */
  readonly selectedDate = signal<string | null>(null);
  /** Valid date currently requested by the user while its response is pending. */
  readonly requestedDate = signal(apodToday());
  readonly currentPicture = signal<ApodEntry | null>(null);
  readonly loading = signal(false);
  readonly error = signal<ApodRequestError | null>(null);

  readonly searchQuery = signal('');
  readonly searchResults = signal<readonly ApodEntry[]>([]);
  readonly searchLoading = signal(false);
  readonly searchError = signal<ApodRequestError | null>(null);

  constructor(private readonly http: HttpClient) {
    this.pictureRequests
      .pipe(
        switchMap((request) => {
          if (!request.endpoint) {
            return EMPTY;
          }

          this.loading.set(true);
          this.error.set(null);
          this.currentPicture.set(null);

          return this.http.get<ApodEntry>(request.endpoint).pipe(
            map((entry): PictureResult => ({ entry })),
            catchError((error: unknown) => of({ error: toRequestError(error) } as PictureResult))
          );
        })
      )
      .subscribe((result) => {
        this.loading.set(false);
        if ('error' in result) {
          this.error.set(result.error);
          return;
        }

        this.selectedDate.set(result.entry.date);
        this.requestedDate.set(result.entry.date);
        this.currentPicture.set(result.entry);
      });

    this.searchRequests
      .pipe(
        switchMap((query) => {
          if (!query) {
            this.searchLoading.set(false);
            this.searchError.set(null);
            this.searchResults.set([]);
            return EMPTY;
          }

          this.searchLoading.set(true);
          this.searchError.set(null);
          this.searchResults.set([]);
          const params = new HttpParams()
            .set('q', query)
            .set('page', '1')
            .set('pageSize', String(APOD_SEARCH_PAGE_SIZE));

          return this.http.get<readonly ApodEntry[]>('/api/apod/search', { params }).pipe(
            map(
              (entries): SearchResult => ({
                entries
              })
            ),
            catchError((error: unknown) => of({ error: toRequestError(error) } as SearchResult))
          );
        })
      )
      .subscribe((result) => {
        this.searchLoading.set(false);
        if ('error' in result) {
          this.searchError.set(result.error);
          return;
        }

        this.searchResults.set(result.entries);
      });

  }

  /** Loads the backend's product-calendar picture of the day. */
  loadToday(): void {
    this.pictureRequests.next({ endpoint: '/api/apod/today' });
  }

  /** Loads one valid calendar date and cancels an earlier in-flight date request. */
  selectDate(date: string): void {
    if (!isApodDate(date)) {
      // Send an empty request through switchMap solely to cancel any earlier
      // in-flight date/today request before publishing the validation state.
      this.pictureRequests.next({ endpoint: '' });
      this.loading.set(false);
      this.error.set({
        code: 'invalid_apod_date',
        message: `Choose a date from ${APOD_FIRST_DATE} through today.`
      });
      return;
    }

    this.requestedDate.set(date);
    this.pictureRequests.next({ endpoint: `/api/apod/date/${date}` });
  }

  retrySelectedDate(): void {
    this.selectDate(this.requestedDate());
  }

  /** Commits a debounced query from SearchBar and cancels any stale HTTP search. */
  setSearchQuery(query: string): void {
    this.searchQuery.set(query);
    this.searchRequests.next(query.trim());
  }

  retrySearch(): void {
    this.searchRequests.next(this.searchQuery().trim());
  }

}

/**
 * Returns the date the product can currently request from APOD.
 *
 * This deliberately uses the product's Argentina calendar instead of the
 * browser's local timezone or UTC. `now` keeps boundary tests independent of
 * the machine and browser running them.
 */
export function apodToday(now: Date = new Date()): string {
  const dateParts = apodCalendarFormatter.formatToParts(now);
  const year = dateParts.find((part) => part.type === 'year')?.value;
  const month = dateParts.find((part) => part.type === 'month')?.value;
  const day = dateParts.find((part) => part.type === 'day')?.value;

  if (!year || !month || !day) {
    throw new Error('The APOD product calendar could not format a complete date.');
  }

  return `${year}-${month}-${day}`;
}

export function isApodDate(value: string, now: Date = new Date()): boolean {
  if (!DATE_PATTERN.test(value) || value < APOD_FIRST_DATE || value > apodToday(now)) {
    return false;
  }

  const [year, month, day] = value.split('-').map(Number);
  const utcDate = new Date(Date.UTC(year, month - 1, day));
  return (
    utcDate.getUTCFullYear() === year &&
    utcDate.getUTCMonth() === month - 1 &&
    utcDate.getUTCDate() === day
  );
}

function toRequestError(error: unknown): ApodRequestError {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { code?: unknown; detail?: unknown; title?: unknown } | null;
    const code = typeof body?.code === 'string' ? body.code : null;
    const message =
      typeof body?.detail === 'string'
        ? body.detail
        : typeof body?.title === 'string'
          ? body.title
          : 'The astronomy service is unavailable. Try again.';
    return { code, message };
  }

  return { code: null, message: 'The astronomy service is unavailable. Try again.' };
}
