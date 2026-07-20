import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { EMPTY, Subject, catchError, map, of, switchMap } from 'rxjs';

import type { ApodEntry } from '../models/apod.model';

export const APOD_FIRST_DATE = '1995-06-16';
export const APOD_SEARCH_PAGE_SIZE = 12;

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

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
  readonly requestedDate = signal(utcToday());
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

  /** Loads the backend's UTC picture of the day. */
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

export function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

export function isApodDate(value: string): boolean {
  if (!DATE_PATTERN.test(value) || value < APOD_FIRST_DATE || value > utcToday()) {
    return false;
  }

  return new Date(`${value}T00:00:00.000Z`).toISOString().slice(0, 10) === value;
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
