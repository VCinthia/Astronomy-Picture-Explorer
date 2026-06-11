import { TestBed } from '@angular/core/testing';

import { AstronomyService } from './astronomy.service';

describe('AstronomyService', () => {
  let service: AstronomyService;

  beforeEach(() => {
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

    it('returns a video entry with a thumbnail_url', () => {
      const entry = service.getByDate('2026-05-24');

      expect(entry?.media_type).toBe('video');
      expect(entry?.thumbnail_url).toBeTruthy();
      expect(entry?.url).toMatch(/\.mp4$/);
    });
  });

  describe('archive shape', () => {
    it('contains at least 15 entries', () => {
      expect(service.availableDates.length).toBeGreaterThanOrEqual(15);
    });

    it('includes the two required dates and at least one video', () => {
      expect(service.hasDate('2004-01-16')).toBeTrue();
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
});
