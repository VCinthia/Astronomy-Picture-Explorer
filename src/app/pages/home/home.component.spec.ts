import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import type { ApodEntry } from '../../models/apod.model';
import { HomeComponent } from './home.component';

const entry: ApodEntry = {
  date: '2026-05-22',
  title: 'The Nebulous Realm of WR 134',
  explanation: 'A ring-like nebula shaped by a Wolf-Rayet star.',
  media_type: 'image',
  url: 'https://example.test/wr134.jpg',
  hdurl: null,
  thumbnail_url: null,
  copyright: null
};

describe('HomeComponent', () => {
  let fixture: ComponentFixture<HomeComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HomeComponent);
  });

  afterEach(() => http.verify({ ignoreCancelled: true }));

  it('loads today through the public API when the lazy home view initializes', () => {
    fixture.detectChanges();
    http.expectOne('/api/apod/today').flush(entry);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-picture-card')).not.toBeNull();
  });

  it('places accessible date controls over the displayed picture and requests the next UTC date', () => {
    fixture.detectChanges();
    http.expectOne('/api/apod/today').flush(entry);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const controls = element.querySelector('[role="group"][aria-label="Step through dates"]');
    const mediaContainer = controls?.parentElement;
    expect(mediaContainer?.classList).toContain('relative');
    expect(controls?.classList).toContain('bottom-4');
    expect(controls?.classList).toContain('right-4');
    expect(controls?.querySelector('button[aria-label="Previous date"] svg')).not.toBeNull();
    expect(controls?.querySelector('button[aria-label="Next date"] svg')).not.toBeNull();

    (controls?.querySelector('button[aria-label="Next date"]') as HTMLButtonElement).click();
    http.expectOne('/api/apod/date/2026-05-23').flush({ ...entry, date: '2026-05-23' });
  });

  it('communicates a recoverable cold-start failure and retries today', () => {
    fixture.detectChanges();
    http.expectOne('/api/apod/today').flush(
      { code: 'apod_upstream_unavailable', detail: 'Try again later.' },
      { status: 503, statusText: 'Service Unavailable' }
    );
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain("Today's picture is unavailable.");
    (element.querySelector('button') as HTMLButtonElement).click();
    http.expectOne('/api/apod/today').flush(entry);
  });
});
