import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import type { ApodEntry } from '../../models/apod.model';
import { AuthSessionChange, AuthUser, AuthService } from '../../services/auth.service';
import { FavoritesService } from '../../services/favorites.service';
import { PictureCardComponent } from './picture-card.component';

class AuthSessionStub {
  readonly currentUser = signal<AuthUser | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly sessionChange = signal<AuthSessionChange>({ previousUserId: null, currentUser: null });

  signIn(user: AuthUser): void {
    this.currentUser.set(user);
    this.sessionChange.set({ previousUserId: null, currentUser: user });
  }
}

const imageEntry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: 'https://example.test/wr134-hd.jpg',
  thumbnail_url: null,
  copyright: 'Luigi Morrone'
};

const videoEntry: ApodEntry = {
  date: '2026-05-24',
  title: 'A Martian Eclipse: Phobos Crosses the Sun',
  explanation: 'Phobos transits the Sun as seen from Mars.',
  media_type: 'video',
  url: 'https://example.test/phobos.mp4',
  hdurl: null,
  thumbnail_url: 'https://example.test/phobos.jpg',
  copyright: null
};

function renderWith(entry: ApodEntry): HTMLElement {
  const fixture = TestBed.createComponent(PictureCardComponent);
  fixture.componentRef.setInput('entry', entry);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('PictureCardComponent', () => {
  let http: HttpTestingController;
  let auth: AuthSessionStub;

  beforeEach(async () => {
    auth = new AuthSessionStub();
    await TestBed.configureTestingModule({
      imports: [PictureCardComponent],
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

    it('uses the authenticated favorite API with pending accessible semantics', () => {
      const service = TestBed.inject(FavoritesService);
      auth.signIn({ id: 'alice', email: 'alice@example.test' });
      TestBed.flushEffects();
      http.expectOne('/api/favorites').flush([]);
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
      fixture.detectChanges();

      expect(button.disabled).toBeTrue();
      expect(button.getAttribute('aria-busy')).toBe('true');
      const request = http.expectOne('/api/favorites');
      expect(request.request.body).toEqual({ apod_date: imageEntry.date });
      request.flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();

      expect(service.isFavorite(imageEntry.date)).toBeTrue();
      expect(button.getAttribute('aria-pressed')).toBe('true');
      expect(button.getAttribute('aria-label')).toContain('Remove');
      expect(button.classList).toContain('text-accent');
      expect(button.classList).not.toContain('text-content-secondary');
      expect(icon.getAttribute('fill')).toBe('currentColor');
      expect(icon.getAttribute('stroke')).toBe('currentColor');
      button.click();
      fixture.detectChanges();

      const deleteRequest = http.expectOne(`/api/favorites/${imageEntry.date}`);
      expect(deleteRequest.request.method).toBe('DELETE');
      deleteRequest.flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();

      expect(service.isFavorite(imageEntry.date)).toBeFalse();
      expect(button.getAttribute('aria-pressed')).toBe('false');
      expect(button.classList).toContain('text-content-secondary');
      expect(button.classList).not.toContain('text-accent');
      expect(icon.getAttribute('fill')).toBe('none');
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

    it('keeps an unthumbnailed video actionable instead of covering it with a spinner', () => {
      const el = renderWith({ ...videoEntry, thumbnail_url: null });

      expect(el.querySelector('img')).toBeNull();
      expect(el.querySelector('a[href="https://example.test/phobos.mp4"]')).not.toBeNull();
      expect(el.querySelector('.animate-spin')).toBeNull();
    });
  });
});
