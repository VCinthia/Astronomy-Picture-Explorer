import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  let auth: AuthService;
  let client: HttpClient;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    auth = TestBed.inject(AuthService);
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('attaches Bearer only to relative /api requests and never to auth or foreign requests', () => {
    authenticate('access-token');

    client.get('/api/favorites').subscribe();
    const apiRequest = http.expectOne('/api/favorites');
    expect(apiRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(apiRequest.request.headers.keys()).not.toContain('RETRIED_AFTER_REFRESH');
    apiRequest.flush([]);

    client.post('/auth/logout', {}).subscribe({ error: () => undefined });
    const logoutRequest = http.expectOne('/auth/logout');
    expect(logoutRequest.request.headers.has('Authorization')).toBeFalse();
    logoutRequest.flush({}, { status: 401, statusText: 'Unauthorized' });

    client.get('https://api.nasa.gov/planetary/apod').subscribe({ error: () => undefined });
    const foreignRequest = http.expectOne('https://api.nasa.gov/planetary/apod');
    expect(foreignRequest.request.headers.has('Authorization')).toBeFalse();
    foreignRequest.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(http.match('/auth/refresh').length).toBe(0);
  });

  it('shares one refresh for concurrent 401 responses and retries every original request once', () => {
    authenticate('expired-token');
    const successes: string[] = [];

    client.get('/api/first').subscribe(() => successes.push('first'));
    client.get('/api/second').subscribe(() => successes.push('second'));
    const first = http.expectOne('/api/first');
    const second = http.expectOne('/api/second');
    first.flush({}, { status: 401, statusText: 'Unauthorized' });
    second.flush({}, { status: 401, statusText: 'Unauthorized' });

    const refreshes = http.match('/auth/refresh');
    expect(refreshes.length).toBe(1);
    expect(refreshes[0].request.headers.has('Authorization')).toBeFalse();
    refreshes[0].flush(sessionResponse('fresh-token'));

    const firstRetry = http.expectOne('/api/first');
    const secondRetry = http.expectOne('/api/second');
    expect(firstRetry.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    expect(secondRetry.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    firstRetry.flush({});
    secondRetry.flush({});

    expect(successes).toEqual(['first', 'second']);
    expect(http.match('/auth/refresh').length).toBe(0);
  });

  it('clears state, rejects the failed queue and navigates to login once when refresh fails', () => {
    authenticate('expired-token');
    const navigate = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    let errors = 0;

    client.get('/api/first').subscribe({ error: () => errors += 1 });
    client.get('/api/second').subscribe({ error: () => errors += 1 });
    http.expectOne('/api/first').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/second').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/auth/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(errors).toBe(2);
    expect(auth.isAuthenticated()).toBeFalse();
    expect(navigate).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/' } });
    expect(http.match('/auth/refresh').length).toBe(0);
  });

  it('does not refresh again when the one permitted retry also returns 401', () => {
    authenticate('expired-token');
    let errors = 0;

    client.get('/api/favorites').subscribe({ error: () => errors += 1 });
    http.expectOne('/api/favorites').flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/auth/refresh').flush(sessionResponse('fresh-token'));
    http.expectOne('/api/favorites').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(errors).toBe(1);
    expect(http.match('/auth/refresh').length).toBe(0);
  });

  it('does not retry an old 401 with a new login token after logout and stale refresh success', () => {
    authenticate('token-a');
    let errors = 0;

    client.get('/api/favorites').subscribe({ error: () => errors += 1 });
    http.expectOne('/api/favorites').flush({}, { status: 401, statusText: 'Unauthorized' });
    const staleRefresh = http.expectOne('/auth/refresh');
    logout();
    authenticate('token-b');

    staleRefresh.flush(sessionResponse('stale-token'));

    expect(errors).toBe(1);
    expect(auth.accessToken()).toBe('token-b');
    expect(http.match('/api/favorites').length).toBe(0);
  });

  it('does not clear or redirect a new login when an old refresh fails after logout', () => {
    authenticate('token-a');
    const navigate = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    let errors = 0;

    client.get('/api/favorites').subscribe({ error: () => errors += 1 });
    http.expectOne('/api/favorites').flush({}, { status: 401, statusText: 'Unauthorized' });
    const staleRefresh = http.expectOne('/auth/refresh');
    logout();
    authenticate('token-b');

    staleRefresh.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(errors).toBe(1);
    expect(auth.accessToken()).toBe('token-b');
    expect(auth.isAuthenticated()).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();
  });

  function authenticate(accessToken: string): void {
    auth.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe();
    http.expectOne('/auth/login').flush(sessionResponse(accessToken));
  }

  function logout(): void {
    auth.logout().subscribe();
    http.expectOne('/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
  }

  function sessionResponse(accessToken: string) {
    return {
      accessToken,
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    };
  }
});
