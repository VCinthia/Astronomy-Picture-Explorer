import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, defer, finalize, tap, throwError } from 'rxjs';

/** The authenticated user shape returned by the application-owned session API. */
export interface AuthUser {
  id: string;
  email: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface ResendConfirmationRequest {
  email: string;
}

export interface ConfirmEmailRequest {
  userId: string;
  code: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** Generic anti-enumeration response shared by register and resend endpoints. */
export interface AccountRequestAcceptedResponse {
  message: string;
}

export interface SessionResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** Safe, typed representation of RFC 7807 responses returned by this API. */
export interface AuthProblem {
  status: number;
  title: string;
  detail: string | null;
  type: string | null;
  code: string | null;
  errors: Readonly<Record<string, readonly string[]>> | null;
}

/**
 * Owns the browser's transient account state. The access JWT intentionally remains
 * in a signal only; W9 adds refresh, interception and session bootstrap.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUserState = signal<AuthUser | null>(null);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly pendingOperations = signal(0);
  private readonly errorState = signal<AuthProblem | null>(null);

  readonly currentUser = this.currentUserState.asReadonly();
  readonly accessToken = this.accessTokenState.asReadonly();
  readonly isAuthenticated = computed(() =>
    this.currentUserState() !== null && this.accessTokenState() !== null
  );
  readonly loading = computed(() => this.pendingOperations() > 0);
  readonly error = this.errorState.asReadonly();

  register(request: RegisterRequest): Observable<AccountRequestAcceptedResponse> {
    return this.track(
      this.http.post<AccountRequestAcceptedResponse>('/auth/register', request)
    );
  }

  resendConfirmation(
    request: ResendConfirmationRequest
  ): Observable<AccountRequestAcceptedResponse> {
    return this.track(
      this.http.post<AccountRequestAcceptedResponse>('/auth/resend-confirmation', request)
    );
  }

  confirmEmail(request: ConfirmEmailRequest): Observable<void> {
    return this.track(this.http.post<void>('/auth/confirm-email', request));
  }

  login(request: LoginRequest): Observable<SessionResponse> {
    return this.track(
      this.http.post<SessionResponse>('/auth/login', request).pipe(
        tap((response) => {
          this.accessTokenState.set(response.accessToken);
          this.currentUserState.set(response.user);
        })
      )
    );
  }

  /** Reserved for W9's refresh failure and logout paths. */
  clearSession(): void {
    this.accessTokenState.set(null);
    this.currentUserState.set(null);
  }

  clearError(): void {
    this.errorState.set(null);
  }

  private track<T>(request: Observable<T>): Observable<T> {
    return defer(() => {
      this.pendingOperations.update((count) => count + 1);
      this.errorState.set(null);

      return request.pipe(
        catchError((error: unknown) => {
          const problem = toAuthProblem(error);
          this.errorState.set(problem);
          return throwError(() => problem);
        }),
        finalize(() => this.pendingOperations.update((count) => Math.max(0, count - 1)))
      );
    });
  }
}

function toAuthProblem(error: unknown): AuthProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      status: 0,
      title: 'Unable to reach the service.',
      detail: null,
      type: null,
      code: null,
      errors: null
    };
  }

  const body = isRecord(error.error) ? error.error : null;
  return {
    status: error.status,
    title: readString(body?.['title']) ?? 'Unable to complete the request.',
    detail: readString(body?.['detail']),
    type: readString(body?.['type']),
    code: readString(body?.['code']),
    errors: readValidationErrors(body?.['errors'])
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readString(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function readValidationErrors(value: unknown): Readonly<Record<string, readonly string[]>> | null {
  if (!isRecord(value)) {
    return null;
  }

  const entries = Object.entries(value)
    .map(([key, messages]) => [
      key,
      Array.isArray(messages) ? messages.filter((message): message is string => typeof message === 'string') : []
    ] as const)
    .filter(([, messages]) => messages.length > 0);

  return entries.length > 0 ? Object.fromEntries(entries) : null;
}
