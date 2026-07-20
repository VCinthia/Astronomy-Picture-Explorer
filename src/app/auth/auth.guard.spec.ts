import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { Observable } from 'rxjs';

import { authGuard } from './auth.guard';

describe('authGuard', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('waits for bootstrap and redirects an anonymous favorites visit with its return URL', () => {
    let result: boolean | UrlTree | undefined;
    const guardResult = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/favorites' } as never)
    ) as Observable<boolean | UrlTree>;
    guardResult.subscribe((value) => result = value);

    const refresh = http.expectOne('/auth/refresh');
    refresh.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Ffavorites');
  });

  it('allows favorites after the root session bootstrap restores a user', () => {
    let result: boolean | UrlTree | undefined;
    const guardResult = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/favorites' } as never)
    ) as Observable<boolean | UrlTree>;
    guardResult.subscribe((value) => result = value);

    http.expectOne('/auth/refresh').flush({
      accessToken: 'restored-token',
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    });

    expect(result).toBeTrue();
  });
});
