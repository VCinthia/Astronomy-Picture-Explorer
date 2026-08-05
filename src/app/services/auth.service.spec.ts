import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';

import { AuthProblem, AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('posts the typed register contract and exposes the generic accepted response', () => {
    let message = '';
    service.register({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe((response) => {
      message = response.message;
    });

    expect(service.loading()).toBeTrue();
    const request = http.expectOne('/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'astro@example.test', password: 'Valid1!Password' });
    request.flush({ message: 'If the address can receive a confirmation email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });

    expect(message).toContain('confirmation email');
    expect(service.loading()).toBeFalse();
  });

  it('posts the resend and confirmation contracts to same-origin auth endpoints', () => {
    service.resendConfirmation({ email: 'astro@example.test' }).subscribe();
    const resend = http.expectOne('/auth/resend-confirmation');
    expect(resend.request.method).toBe('POST');
    expect(resend.request.body).toEqual({ email: 'astro@example.test' });
    resend.flush({ message: 'If the address can receive a confirmation email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });

    service.confirmEmail({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'Q29uZmlybWF0aW9uLXRva2Vu'
    }).subscribe();
    const confirmation = http.expectOne('/auth/confirm-email');
    expect(confirmation.request.method).toBe('POST');
    expect(confirmation.request.body).toEqual({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'Q29uZmlybWF0aW9uLXRva2Vu'
    });
    confirmation.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('posts password recovery requests without persisting the email or reset capability', () => {
    let message = '';
    service.forgotPassword({ email: 'astro@example.test' }).subscribe((response) => message = response.message);
    const request = http.expectOne('/auth/forgot-password');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'astro@example.test' });
    request.flush({ message: 'If the address can receive a password reset email, a message will be sent.' }, {
      status: 202,
      statusText: 'Accepted'
    });

    expect(message).toContain('password reset email');
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('clears the in-memory session after a successful password reset without auto-login', () => {
    service.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe();
    http.expectOne('/auth/login').flush(sessionResponse('access-token', 'first-user'));
    let resetComplete = false;

    service.resetPassword({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4',
      password: 'New2!Password'
    }).subscribe(() => resetComplete = true);
    const reset = http.expectOne('/auth/reset-password');
    expect(reset.request.method).toBe('POST');
    expect(reset.request.body).toEqual({
      userId: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      code: 'UmVzZXQtdG9rZW4',
      password: 'New2!Password'
    });
    reset.flush(null, { status: 204, statusText: 'No Content' });

    expect(resetComplete).toBeTrue();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.accessToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('keeps a successful access JWT and user only in signals, never browser storage', () => {
    const localSetItem = spyOn(localStorage, 'setItem').and.callThrough();
    const sessionSetItem = spyOn(sessionStorage, 'setItem').and.callThrough();
    service.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe();
    const request = http.expectOne('/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'astro@example.test', password: 'Valid1!Password' });
    request.flush({
      accessToken: 'header.payload.signature',
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4', email: 'astro@example.test' }
    });

    expect(service.accessToken()).toBe('header.payload.signature');
    expect(service.currentUser()).toEqual({
      id: '5c409cbf-b9cc-4afe-a55b-a8b7c4f1aac4',
      email: 'astro@example.test'
    });
    expect(service.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
    expect(JSON.stringify(localStorage)).not.toContain('header.payload.signature');
    expect(JSON.stringify(sessionStorage)).not.toContain('header.payload.signature');
    expect(localSetItem).not.toHaveBeenCalled();
    expect(sessionSetItem).not.toHaveBeenCalled();
  });

  it('maps ProblemDetails code and validation errors without exposing an untyped HttpErrorResponse', () => {
    let problem: AuthProblem | null = null;
    service.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe({
      error: (error: AuthProblem) => problem = error
    });

    const request = http.expectOne('/auth/login');
    request.flush({
      type: 'https://httpstatuses.com/403',
      title: 'Email confirmation required.',
      detail: 'Confirm your email before signing in.',
      status: 403,
      code: 'email_unconfirmed',
      errors: { email: ['Confirm the address first.'] }
    }, { status: 403, statusText: 'Forbidden' });

    expect(problem).toEqual(jasmine.objectContaining({
      status: 403,
      code: 'email_unconfirmed',
      errors: { email: ['Confirm the address first.'] }
    }));
    expect(service.error()).toEqual(problem);
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('bootstraps with exactly one refresh for the application lifetime and restores memory state', () => {
    let firstComplete = false;
    let secondComplete = false;

    service.bootstrap().subscribe(() => firstComplete = true);
    service.bootstrap().subscribe(() => secondComplete = true);

    const refreshes = http.match('/auth/refresh');
    expect(refreshes.length).toBe(1);
    expect(service.sessionState()).toBe('checking');
    refreshes[0].flush(sessionResponse('boot-token', 'first-user'));

    expect(firstComplete).toBeTrue();
    expect(secondComplete).toBeTrue();
    expect(service.sessionState()).toBe('auth');
    expect(service.accessToken()).toBe('boot-token');
    expect(service.currentUser()?.id).toBe('first-user');

    service.bootstrap().subscribe();
    expect(http.match('/auth/refresh').length).toBe(0);
  });

  it('settles an anonymous bootstrap locally without storing a token or surfacing a redirect concern', () => {
    service.bootstrap().subscribe();

    const refresh = http.expectOne('/auth/refresh');
    refresh.flush({ code: 'invalid_refresh_token' }, { status: 401, statusText: 'Unauthorized' });

    expect(service.sessionState()).toBe('anon');
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.accessToken()).toBeNull();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('shares one refresh operation and exposes a session-change signal for a user switch and logout', () => {
    const changes: string[] = [];
    service.login({ email: 'first@example.test', password: 'Valid1!Password' }).subscribe();
    http.expectOne('/auth/login').flush(sessionResponse('first-token', 'first-user'));
    changes.push(`${service.sessionChange().previousUserId}:${service.sessionChange().currentUser?.id}`);

    service.refreshSession().subscribe();
    service.refreshSession().subscribe();
    const refreshes = http.match('/auth/refresh');
    expect(refreshes.length).toBe(1);
    refreshes[0].flush(sessionResponse('second-token', 'second-user'));
    changes.push(`${service.sessionChange().previousUserId}:${service.sessionChange().currentUser?.id}`);

    service.logout().subscribe();
    const logout = http.expectOne('/auth/logout');
    expect(service.isAuthenticated()).toBeFalse();
    logout.error(new ProgressEvent('network error'));
    changes.push(`${service.sessionChange().previousUserId}:${service.sessionChange().currentUser?.id}`);

    expect(service.isAuthenticated()).toBeFalse();
    expect(changes).toEqual([
      'null:first-user',
      'first-user:second-user',
      'second-user:undefined'
    ]);
  });

  it('does not let a refresh that started before logout restore the cleared session', () => {
    service.login({ email: 'astro@example.test', password: 'Valid1!Password' }).subscribe();
    http.expectOne('/auth/login').flush(sessionResponse('access-token', 'first-user'));

    service.refreshSession().subscribe();
    const refresh = http.expectOne('/auth/refresh');
    service.logout().subscribe();
    const logout = http.expectOne('/auth/logout');
    expect(service.isAuthenticated()).toBeFalse();

    refresh.flush(sessionResponse('stale-token', 'first-user'));
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.accessToken()).toBeNull();
    logout.flush(null, { status: 204, statusText: 'No Content' });
  });

  function sessionResponse(accessToken: string, userId: string) {
    return {
      accessToken,
      expiresAt: '2026-07-20T20:00:00Z',
      user: { id: userId, email: `${userId}@example.test` }
    };
  }
});
