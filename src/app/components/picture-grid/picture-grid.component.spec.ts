import { TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../../models/apod.model';
import { PictureGridComponent } from './picture-grid.component';

const entries: ApodEntry[] = [
  {
    date: '2026-05-22',
    title: 'The Nebulous Realm of WR 134',
    explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
    media_type: 'image',
    service_version: 'v1',
    url: 'https://example.test/wr134.jpg',
    hdurl: 'https://example.test/wr134-hd.jpg'
  },
  {
    date: '2026-05-24',
    title: 'A Martian Eclipse',
    explanation: 'Phobos transits the Sun as seen from Mars.',
    media_type: 'video',
    service_version: 'v1',
    url: 'https://example.test/phobos.mp4',
    thumbnail_url: 'https://example.test/phobos.jpg'
  }
];

describe('PictureGridComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PictureGridComponent] }).compileComponents();
  });

  function render(items: readonly ApodEntry[]): HTMLElement {
    const fixture = TestBed.createComponent(PictureGridComponent);
    fixture.componentRef.setInput('entries', items);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one compact card for every entry', () => {
    const element = render(entries);
    const cards = element.querySelectorAll('article');

    expect(cards.length).toBe(2);
    expect(cards[0].textContent).toContain('The Nebulous Realm of WR 134');
    expect(cards[0].textContent).toContain('May 22, 2026');
    expect(cards[1].textContent).toContain('A Martian Eclipse');
  });

  it('uses the compact media token with an overlaid date and a two-line title', () => {
    const element = render(entries);
    const media = element.querySelector('article a > div');
    const date = media?.querySelector('span.absolute.bottom-3.left-3');
    const title = element.querySelector('article h2');

    expect(media?.classList).toContain('h-grid-media');
    expect(media?.classList).not.toContain('aspect-video');
    expect(date?.textContent).toContain('May 22, 2026');
    expect(title?.classList).toContain('line-clamp-2');
    expect(element.querySelector('article p')).toBeNull();
  });

  it('uses a single mobile column and three columns from the desktop breakpoint', () => {
    const grid = render(entries).querySelector('div.grid');

    expect(grid?.classList).toContain('grid-cols-1');
    expect(grid?.classList).toContain('md:grid-cols-3');
  });

  it('renders image and video previews with their correct destinations', () => {
    const element = render(entries);
    const images = element.querySelectorAll('img');
    const links = element.querySelectorAll('a');

    expect(images[0].getAttribute('src')).toBe(entries[0].url);
    expect(links[0].getAttribute('href')).toBe(entries[0].hdurl!);
    expect(images[1].getAttribute('src')).toBe(entries[1].thumbnail_url!);
    expect(links[1].getAttribute('href')).toBe(entries[1].url);
    expect(element.textContent).toContain('Video');
    for (const image of images) {
      expect(image.getAttribute('loading')).toBe('lazy');
      expect(image.getAttribute('decoding')).toBe('async');
    }
  });

  it('renders an empty grid when there are no entries', () => {
    expect(render([]).querySelectorAll('article').length).toBe(0);
  });
});
