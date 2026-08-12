import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { ConfirmEmailComponent } from './confirm-email.component';

@Component({
  changeDetection: ChangeDetectionStrategy.Default,
  template: ''
})
class LoginRouteComponent {}

describe('ConfirmEmailComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('shows an accessible error and makes zero requests when the link is missing data', async () => {
    const { fixture, http } = await createFixture({ userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4' });
    fixture.detectChanges();

    http.expectNone('/auth/confirm-email');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent)
      .toContain('invalid or incomplete');
  });

  it('rejects malformed base64url codes before calling the backend', async () => {
    const { fixture, http } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'not+base64'
    });
    fixture.detectChanges();

    http.expectNone('/auth/confirm-email');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).not.toBeNull();
  });

  it('rejects base64url lengths that cannot be decoded before calling the backend', async () => {
    const { fixture, http } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'A'
    });
    fixture.detectChanges();

    http.expectNone('/auth/confirm-email');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).not.toBeNull();
  });

  it('uses POST only for a valid link, scrubs the URL, and redirects to login after success', async () => {
    const { fixture, http, location } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'Q29uZmlybWF0aW9uLXRva2Vu'
    });
    const replaceState = spyOn(location, 'replaceState').and.callThrough();
    fixture.detectChanges();

    const request = http.expectOne('/auth/confirm-email');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'Q29uZmlybWF0aW9uLXRva2Vu'
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(replaceState).toHaveBeenCalledWith('/confirm-email');
    expect(location.path()).toBe('/login');
  });

  it('renders an accessible error after the valid POST fails', async () => {
    const { fixture, http } = await createFixture({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'Q29uZmlybWF0aW9uLXRva2Vu'
    });
    const location = TestBed.inject(Location);
    const replaceState = spyOn(location, 'replaceState').and.callThrough();
    fixture.detectChanges();
    http.expectOne('/auth/confirm-email').flush({
      title: 'Unable to confirm email.',
      status: 400
    }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(replaceState).toHaveBeenCalledWith('/confirm-email');
    expect(location.path()).toBe('/confirm-email');
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent)
      .toContain('could not confirm');
  });

  async function createFixture(query: Record<string, string>) {
    await TestBed.configureTestingModule({
      imports: [ConfirmEmailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideLocationMocks(),
        provideRouter([{ path: 'login', component: LoginRouteComponent }]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(query) } }
        }
      ]
    }).compileComponents();

    return {
      fixture: TestBed.createComponent(ConfirmEmailComponent),
      http: TestBed.inject(HttpTestingController),
      location: TestBed.inject(Location)
    };
  }
});
