import { TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../../models/apod.model';
import { PictureCardComponent } from './picture-card.component';

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

    it('does not render the color palette for video', () => {
      const el = renderWith(videoEntry);

      expect(el.querySelector('app-color-palette')).toBeNull();
      expect(el.textContent).toContain('Not available for video content');
    });
  });
});
