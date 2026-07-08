import { TestBed } from '@angular/core/testing';

import { AstronomyService } from './astronomy.service';

const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';

describe('AstronomyService', () => {
  let service: AstronomyService;

  beforeEach(() => {
    localStorage.removeItem(FAVORITES_STORAGE_KEY);
    TestBed.configureTestingModule({});
    service = TestBed.inject(AstronomyService);
  });

  describe('getByDate', () => {
    it('returns the entry for an existing image date', () => {
      const entry = service.getByDate('2026-05-22');

      expect(entry).toBeDefined();
      expect(entry?.title).toBe('The Nebulous Realm of WR 134');
      expect(entry?.media_type).toBe('image');
      expect(entry?.hdurl).toContain('WR134morrone2048.jpg');
    });

    it('returns undefined for a date with no entry', () => {
      expect(service.getByDate('1999-12-31')).toBeUndefined();
    });

    it('does not treat inherited object properties as archive entries', () => {
      expect(service.hasDate('__proto__')).toBeFalse();
      expect(service.hasDate('toString')).toBeFalse();
      expect(service.getByDate('__proto__')).toBeUndefined();
    });

    it('returns a video entry with a thumbnail_url', () => {
      const entry = service.getByDate('2026-05-24');

      expect(entry?.media_type).toBe('video');
      expect(entry?.thumbnail_url).toBeTruthy();
      expect(entry?.url).toMatch(/\.mp4$/);
    });
  });

  describe('archive shape', () => {
    it('contains at least 8 entries', () => {
      expect(service.availableDates.length).toBeGreaterThanOrEqual(8);
    });

    it('includes the WR 134 entry and at least one video', () => {
      expect(service.hasDate('2026-05-22')).toBeTrue();

      const videos = service.availableDates
        .map((date) => service.getByDate(date))
        .filter((entry) => entry?.media_type === 'video');
      expect(videos.length).toBeGreaterThanOrEqual(1);
    });

    it('every entry has the required contract fields', () => {
      for (const date of service.availableDates) {
        const entry = service.getByDate(date);

        expect(entry).toBeDefined();
        if (!entry) {
          continue;
        }
        expect(entry.date).toBe(date);
        expect(entry.title).toBeTruthy();
        expect(entry.explanation).toBeTruthy();
        expect(entry.url).toBeTruthy();
        expect(['image', 'video']).toContain(entry.media_type);
        if (entry.media_type === 'video') {
          expect(entry.thumbnail_url).toBeTruthy();
        }
      }
    });
  });

  describe('selection signals', () => {
    it('defaults the selected date to an entry present in the archive', () => {
      expect(service.hasDate(service.selectedDate())).toBeTrue();
    });

    it('drives currentPicture from selectedDate', () => {
      service.selectDate('2026-06-09');
      expect(service.currentPicture()?.title).toBe("Thor's Helmet");

      service.selectDate('1999-12-31');
      expect(service.currentPicture()).toBeUndefined();
    });

    it('starts settled with no error', () => {
      expect(service.loading()).toBeFalse();
      expect(service.error()).toBeNull();
    });
  });

  describe('favorite signals', () => {
    it('toggles valid dates without duplicates', () => {
      service.toggleFavorite('2026-05-22');
      service.toggleFavorite('2026-06-09');

      expect(service.favorites()).toEqual(['2026-05-22', '2026-06-09']);
      expect(service.isFavorite('2026-05-22')).toBeTrue();

      service.toggleFavorite('2026-05-22');

      expect(service.favorites()).toEqual(['2026-06-09']);
      expect(service.isFavorite('2026-05-22')).toBeFalse();
    });

    it('ignores dates that do not exist in the archive', () => {
      service.toggleFavorite('1999-12-31');
      service.toggleFavorite('__proto__');

      expect(service.favorites()).toEqual([]);
    });

    it('persists favorites and restores them in a new service instance', () => {
      service.toggleFavorite('2026-05-22');
      TestBed.flushEffects();

      expect(JSON.parse(localStorage.getItem(FAVORITES_STORAGE_KEY) ?? '[]')).toEqual([
        '2026-05-22'
      ]);

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({});
      service = TestBed.inject(AstronomyService);

      expect(service.favorites()).toEqual(['2026-05-22']);
    });

    it('deduplicates stored dates and discards absent or inherited property names', () => {
      TestBed.resetTestingModule();
      localStorage.setItem(
        FAVORITES_STORAGE_KEY,
        JSON.stringify([
          '2026-05-22',
          '2026-05-22',
          '1999-12-31',
          'toString',
          '2026-06-09'
        ])
      );
      TestBed.configureTestingModule({});

      service = TestBed.inject(AstronomyService);

      expect(service.favorites()).toEqual(['2026-05-22', '2026-06-09']);
      expect(service.isFavorite('toString')).toBeFalse();
    });

    it('falls back to an empty list for corrupt JSON or an invalid shape', () => {
      TestBed.resetTestingModule();
      localStorage.setItem(FAVORITES_STORAGE_KEY, '{not-json');
      TestBed.configureTestingModule({});

      expect(TestBed.inject(AstronomyService).favorites()).toEqual([]);

      TestBed.resetTestingModule();
      localStorage.setItem(FAVORITES_STORAGE_KEY, JSON.stringify(['2026-05-22', 7]));
      TestBed.configureTestingModule({});

      expect(TestBed.inject(AstronomyService).favorites()).toEqual([]);
    });

    it('continues to work in memory when storage writes fail', () => {
      const setItem = spyOn(localStorage, 'setItem').and.throwError('storage denied');

      expect(() => {
        service.toggleFavorite('2026-05-22');
        TestBed.flushEffects();
      }).not.toThrow();
      expect(setItem).toHaveBeenCalled();
      expect(service.favorites()).toEqual(['2026-05-22']);
    });

    it('initializes with an empty list when storage reads fail', () => {
      TestBed.resetTestingModule();
      spyOn(localStorage, 'getItem').and.throwError('storage denied');
      TestBed.configureTestingModule({});

      expect(() => (service = TestBed.inject(AstronomyService))).not.toThrow();
      expect(service.favorites()).toEqual([]);
    });
  });

  describe('search signals', () => {
    it('returns an empty result for an empty or whitespace-only query', () => {
      expect(service.searchResults()).toEqual([]);

      service.searchQuery.set('   ');

      expect(service.searchResults()).toEqual([]);
    });

    it('matches keywords in titles and explanations', () => {
      service.searchQuery.set('Helmet');
      expect(service.searchResults().map((entry) => entry.date)).toEqual(['2026-06-09']);

      service.searchQuery.set('neutron star');
      expect(service.searchResults().map((entry) => entry.date)).toEqual(['2026-06-02']);
    });

    it('trims the query and matches case-insensitively', () => {
      service.searchQuery.set('  wOlF-rAyEt  ');

      expect(service.searchResults().map((entry) => entry.date)).toEqual([
        '2026-05-22',
        '2026-06-09'
      ]);
    });
  });
});
