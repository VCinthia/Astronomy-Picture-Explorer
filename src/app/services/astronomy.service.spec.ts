import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../models/apod.model';
import { AstronomyService, APOD_FIRST_DATE, isApodDate } from './astronomy.service';

const imageEntry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: 'https://example.test/wr134-hd.jpg',
  thumbnail_url: null,
  copyright: null
};
const videoEntry: ApodEntry = {
  date: '2026-05-24',
  title: 'A Martian Eclipse',
  explanation: 'Phobos transits the Sun as seen from Mars.',
  media_type: 'video',
  url: 'https://example.test/phobos.mp4',
  hdurl: null,
  thumbnail_url: 'https://example.test/phobos.jpg',
  copyright: 'NASA'
};

describe('AstronomyService', () => {
  let service: AstronomyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AstronomyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

  it('uses the exact today endpoint and adopts the returned real APOD date', () => {
    service.loadToday();

    http.expectOne('/api/apod/today').flush(imageEntry);

    expect(service.currentPicture()).toEqual(imageEntry);
    expect(service.selectedDate()).toBe(imageEntry.date);
    expect(service.loading()).toBeFalse();
    expect(service.error()).toBeNull();
  });

  it('uses the exact date endpoint and preserves nullable backend fields', () => {
    service.selectDate(videoEntry.date);

    http.expectOne(`/api/apod/date/${videoEntry.date}`).flush(videoEntry);

    expect(service.currentPicture()?.media_type).toBe('video');
    expect(service.currentPicture()?.hdurl).toBeNull();
    expect(service.currentPicture()?.thumbnail_url).toBe(videoEntry.thumbnail_url);
  });

  it('cancels a stale date request before accepting the newest response', () => {
    service.selectDate('2026-05-22');
    const stale = http.expectOne('/api/apod/date/2026-05-22');

    service.selectDate('2026-05-24');
    const current = http.expectOne('/api/apod/date/2026-05-24');

    expect(stale.cancelled).toBeTrue();
    current.flush(videoEntry);
    expect(service.currentPicture()).toEqual(videoEntry);
  });

  it('rejects malformed, impossible, future, and pre-APOD dates without HTTP', () => {
    for (const invalid of ['1995-06-15', 'not-a-date', '2026-02-31', '9999-12-31']) {
      service.selectDate(invalid);
    }

    expect(http.match(() => true)).toEqual([]);
    expect(service.error()?.code).toBe('invalid_apod_date');
    expect(isApodDate(APOD_FIRST_DATE)).toBeTrue();
  });

  it('maps date ProblemDetails into a retryable UI error', () => {
    service.selectDate('2026-05-22');
    http.expectOne('/api/apod/date/2026-05-22').flush(
      { code: 'apod_upstream_timeout', detail: 'The astronomy service did not respond in time.' },
      { status: 504, statusText: 'Gateway Timeout' }
    );

    expect(service.currentPicture()).toBeNull();
    expect(service.error()).toEqual({
      code: 'apod_upstream_timeout',
      message: 'The astronomy service did not respond in time.'
    });
  });

  it('queries the paged backend search endpoint rather than filtering a bundled archive', () => {
    service.setSearchQuery('  nebula  ');

    const request = http.expectOne((candidate) => candidate.url === '/api/apod/search');
    expect(request.request.params.get('q')).toBe('nebula');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush([imageEntry]);

    expect(service.searchResults()).toEqual([imageEntry]);
    expect(service.searchLoading()).toBeFalse();
  });

  it('cancels stale searches and exposes catalog_not_ready as a recoverable error', () => {
    service.setSearchQuery('nebula');
    const stale = http.expectOne((candidate) => candidate.url === '/api/apod/search');
    service.setSearchQuery('galaxy');
    const current = http.expectOne((candidate) => candidate.url === '/api/apod/search');

    expect(stale.cancelled).toBeTrue();
    expect(current.request.params.get('q')).toBe('galaxy');
    current.flush(
      { code: 'catalog_not_ready', detail: 'The historical catalog is still being prepared.' },
      { status: 503, statusText: 'Service Unavailable' }
    );

    expect(service.searchResults()).toEqual([]);
    expect(service.searchError()?.code).toBe('catalog_not_ready');
  });

  it('clears results and cancels an in-flight search when the query is emptied', () => {
    service.setSearchQuery('nebula');
    const request = http.expectOne((candidate) => candidate.url === '/api/apod/search');

    service.setSearchQuery('   ');

    expect(request.cancelled).toBeTrue();
    expect(service.searchResults()).toEqual([]);
    expect(service.searchError()).toBeNull();
  });

});
