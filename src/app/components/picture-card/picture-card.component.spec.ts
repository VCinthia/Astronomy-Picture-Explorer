import { TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../../models/apod.model';
import { AstronomyService } from '../../services/astronomy.service';
import { PictureCardComponent } from './picture-card.component';

const FAVORITES_STORAGE_KEY = 'ape.favorites.v1';

const imageEntry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  service_version: 'v1',
  url: 'https://example.test/wr134.jpg',
  hdurl: 'https://example.test/wr134-hd.jpg',
  copyright: 'Luigi Morrone'
};

const videoEntry: ApodEntry = {
  date: '2026-05-24',
  title: 'A Martian Eclipse: Phobos Crosses the Sun',
  explanation: 'Phobos transits the Sun as seen from Mars.',
  media_type: 'video',
  service_version: 'v1',
  url: 'https://example.test/phobos.mp4',
  thumbnail_url: 'https://example.test/phobos.jpg'
};

function renderWith(entry: ApodEntry): HTMLElement {
  const fixture = TestBed.createComponent(PictureCardComponent);
  fixture.componentRef.setInput('entry', entry);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('PictureCardComponent', () => {
  beforeEach(async () => {
    localStorage.removeItem(FAVORITES_STORAGE_KEY);
    await TestBed.configureTestingModule({ imports: [PictureCardComponent] }).compileComponents();
  });

  describe('image entry', () => {
    it('renders an img with the url and explanation as alt', () => {
      const el = renderWith(imageEntry);
      const img = el.querySelector('img');

      expect(img?.getAttribute('src')).toBe(imageEntry.url);
      expect(img?.getAttribute('alt')).toBe(imageEntry.explanation);
    });

    it('links to the hd image and embeds the color palette', () => {
      const el = renderWith(imageEntry);

      expect(el.querySelector(`a[href="${imageEntry.hdurl}"]`)).not.toBeNull();
      expect(el.querySelector('app-color-palette')).not.toBeNull();
    });

    it('shows title, formatted date and copyright', () => {
      const el = renderWith(imageEntry);

      expect(el.querySelector('h1')?.textContent).toContain('The Nebulous Realm of WR 134');
      expect(el.textContent).toContain('May 22, 2026');
      expect(el.textContent).toContain('Luigi Morrone');
    });

    it('never uses an iframe', () => {
      expect(renderWith(imageEntry).querySelector('iframe')).toBeNull();
    });

    it('toggles and persists its favorite state with accessible semantics', () => {
      const service = TestBed.inject(AstronomyService);
      const fixture = TestBed.createComponent(PictureCardComponent);
      fixture.componentRef.setInput('entry', imageEntry);
      fixture.detectChanges();

      const mediaLink = fixture.nativeElement.querySelector('a') as HTMLAnchorElement;
      const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
      const icon = button.querySelector('svg') as SVGElement;

      expect(button.parentElement).toBe(mediaLink.parentElement);
      expect(button.contains(mediaLink)).toBeFalse();
      expect(button.classList).toContain('size-9');
      expect(icon).not.toBeNull();
      expect(icon.getAttribute('aria-hidden')).toBe('true');
      expect(icon.classList).toContain('size-5');
      expect(icon.getAttribute('fill')).toBe('none');
      expect(icon.getAttribute('stroke')).toBe('currentColor');
      expect(button.textContent).not.toContain('\u2665');
      expect(button.textContent).not.toContain('\u2661');
      expect(button.getAttribute('aria-pressed')).toBe('false');
      expect(button.getAttribute('aria-label')).toContain('Add');
      expect(button.classList).toContain('text-content-secondary');
      expect(button.classList).not.toContain('text-accent');

      button.click();
      TestBed.flushEffects();
      fixture.detectChanges();

      expect(service.isFavorite(imageEntry.date)).toBeTrue();
      expect(button.getAttribute('aria-pressed')).toBe('true');
      expect(button.getAttribute('aria-label')).toContain('Remove');
      expect(button.classList).toContain('text-accent');
      expect(button.classList).not.toContain('text-content-secondary');
      expect(icon.getAttribute('fill')).toBe('currentColor');
      expect(icon.getAttribute('stroke')).toBe('currentColor');
      expect(JSON.parse(localStorage.getItem(FAVORITES_STORAGE_KEY) ?? '[]')).toEqual([
        imageEntry.date
      ]);

      button.click();
      TestBed.flushEffects();
      fixture.detectChanges();

      expect(service.isFavorite(imageEntry.date)).toBeFalse();
      expect(button.getAttribute('aria-pressed')).toBe('false');
      expect(button.classList).toContain('text-content-secondary');
      expect(button.classList).not.toContain('text-accent');
      expect(icon.getAttribute('fill')).toBe('none');
      expect(JSON.parse(localStorage.getItem(FAVORITES_STORAGE_KEY) ?? '[]')).toEqual([]);
    });
  });

  describe('video entry', () => {
    it('renders a video badge and a link to the video without an iframe', () => {
      const el = renderWith(videoEntry);

      expect(el.textContent?.toLowerCase()).toContain('video');
      const link = el.querySelector(`a[href="${videoEntry.url}"]`);
      expect(link).not.toBeNull();
      expect(link?.getAttribute('target')).toBe('_blank');
      expect(el.querySelector('iframe')).toBeNull();
    });

    it('uses an SVG external-link marker instead of a Unicode arrow', () => {
      const el = renderWith(videoEntry);
      const watchLink = Array.from(el.querySelectorAll('a')).find((link) =>
        link.textContent?.includes('Watch video')
      );

      expect(watchLink?.querySelector('svg[aria-hidden="true"]')).not.toBeNull();
      expect(watchLink?.textContent).not.toContain('↗');
    });

    it('does not render the color palette for video', () => {
      const el = renderWith(videoEntry);

      expect(el.querySelector('app-color-palette')).toBeNull();
      expect(el.textContent).toContain('Not available for video content');
    });
  });
});
