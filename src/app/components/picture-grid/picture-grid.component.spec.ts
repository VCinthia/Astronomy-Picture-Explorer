import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import type { ApodEntry } from '../../models/apod.model';
import { AuthSessionChange, AuthUser, AuthService } from '../../services/auth.service';
import { FavoritesService } from '../../services/favorites.service';
import { PictureGridComponent } from './picture-grid.component';

class AuthSessionStub {
  readonly currentUser = signal<AuthUser | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly sessionChange = signal<AuthSessionChange>({ previousUserId: null, currentUser: null });

  signIn(user: AuthUser): void {
    this.currentUser.set(user);
    this.sessionChange.set({ previousUserId: null, currentUser: user });
  }
}

const entries: ApodEntry[] = [
  {
    date: '2026-05-22',
    title: 'The Nebulous Realm of WR 134',
    explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
    media_type: 'image',
    url: 'https://example.test/wr134.jpg',
    hdurl: 'https://example.test/wr134-hd.jpg',
    thumbnail_url: null,
    copyright: null
  },
  {
    date: '2026-05-24',
    title: 'A Martian Eclipse',
    explanation: 'Phobos transits the Sun as seen from Mars.',
    media_type: 'video',
    url: 'https://example.test/phobos.mp4',
    hdurl: null,
    thumbnail_url: 'https://example.test/phobos.jpg',
    copyright: null
  }
];

describe('PictureGridComponent', () => {
  let http: HttpTestingController;
  let auth: AuthSessionStub;

  beforeEach(async () => {
    auth = new AuthSessionStub();
    await TestBed.configureTestingModule({
      imports: [PictureGridComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

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

  it('uses an accessible visual placeholder instead of a video URL when no thumbnail exists', () => {
    const unthumbnailedVideo: ApodEntry = { ...entries[1], thumbnail_url: null };
    const element = render([unthumbnailedVideo]);
    const link = element.querySelector('a') as HTMLAnchorElement;

    expect(element.querySelector('img')).toBeNull();
    expect(link.getAttribute('href')).toBe(unthumbnailedVideo.url);
    expect(link.getAttribute('aria-label')).toBe(`Watch video: ${unthumbnailedVideo.title}`);
    expect(element.querySelector('div[aria-hidden="true"] svg')).not.toBeNull();
    expect(element.textContent).toContain('Video');
  });

  it('renders an empty grid when there are no entries', () => {
    expect(render([]).querySelectorAll('article').length).toBe(0);
  });

  it('keeps the favorite controls outside media links and reflects their pressed state', () => {
    const service = TestBed.inject(FavoritesService);
    auth.signIn({ id: 'alice', email: 'alice@example.test' });
    TestBed.flushEffects();
    http.expectOne('/api/favorites').flush([]);
    const fixture = TestBed.createComponent(PictureGridComponent);
    fixture.componentRef.setInput('entries', entries);
    fixture.detectChanges();

    const firstCard = fixture.nativeElement.querySelector('article') as HTMLElement;
    const mediaLink = firstCard.querySelector('a') as HTMLAnchorElement;
    const button = firstCard.querySelector('button') as HTMLButtonElement;
    const icon = button.querySelector('svg') as SVGElement;

    expect(button.parentElement).toBe(firstCard);
    expect(mediaLink.contains(button)).toBeFalse();
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
    fixture.detectChanges();

    expect(button.disabled).toBeTrue();
    const request = http.expectOne('/api/favorites');
    expect(request.request.body).toEqual({ apod_date: entries[0].date });
    request.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(service.isFavorite(entries[0].date)).toBeTrue();
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.getAttribute('aria-label')).toContain('Remove');
    expect(button.classList).toContain('text-accent');
    expect(button.classList).not.toContain('text-content-secondary');
    expect(icon.getAttribute('fill')).toBe('currentColor');
    expect(icon.getAttribute('stroke')).toBe('currentColor');
  });

  it('labels the anonymous heart as a sign-in CTA without issuing a favorite request', () => {
    const fixture = TestBed.createComponent(PictureGridComponent);
    fixture.componentRef.setInput('entries', entries);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toContain('Sign in');
    button.click();

    expect(http.match('/api/favorites').length).toBe(0);
  });
});
