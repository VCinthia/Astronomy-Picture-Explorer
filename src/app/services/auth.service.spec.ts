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
});
