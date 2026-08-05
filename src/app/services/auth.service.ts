import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, defer, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';

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

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  userId: string;
  code: string;
  password: string;
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

/** The settled result of the one-time refresh attempted while the SPA starts. */
export type AuthSessionState = 'checking' | 'auth' | 'anon';

/**
 * A small, signal-friendly session event for consumers that own user-scoped data.
 * W11 uses this boundary to clear cached favorites when the user changes or logs out.
 */
export interface AuthSessionChange {
  previousUserId: string | null;
  currentUser: AuthUser | null;
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
 * in a signal only; refresh is shared so one cookie rotation serves every concurrent
 * request that observed the same expired access token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUserState = signal<AuthUser | null>(null);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly pendingOperations = signal(0);
  private readonly errorState = signal<AuthProblem | null>(null);
  private readonly sessionStateValue = signal<AuthSessionState>('checking');
  private readonly sessionChangeValue = signal<AuthSessionChange>({
    previousUserId: null,
    currentUser: null
  });
  private bootstrapRequest: Observable<void> | null = null;
  private refreshRequest: Observable<SessionResponse> | null = null;
  private refreshFailureRedirected = false;
  private sessionEpoch = 0;

  readonly currentUser = this.currentUserState.asReadonly();
  readonly accessToken = this.accessTokenState.asReadonly();
  readonly isAuthenticated = computed(() =>
    this.currentUserState() !== null && this.accessTokenState() !== null
  );
  readonly loading = computed(() => this.pendingOperations() > 0);
  readonly error = this.errorState.asReadonly();
  readonly sessionState = this.sessionStateValue.asReadonly();
  readonly sessionChange = this.sessionChangeValue.asReadonly();

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

  forgotPassword(request: ForgotPasswordRequest): Observable<AccountRequestAcceptedResponse> {
    return this.track(
      this.http.post<AccountRequestAcceptedResponse>('/auth/forgot-password', request)
    );
  }

  /**
   * A successful reset is not a login. It invalidates the local memory session before
   * the caller navigates so no prior user state can survive the password change.
   */
  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.track(
      this.http.post<void>('/auth/reset-password', request).pipe(
        tap(() => {
          this.sessionEpoch += 1;
          this.clearSession();
        })
      )
    );
  }

  login(request: LoginRequest): Observable<SessionResponse> {
    const loginEpoch = ++this.sessionEpoch;
    return this.track(
      this.http.post<SessionResponse>('/auth/login', request).pipe(
        tap((response) => {
          if (loginEpoch === this.sessionEpoch) {
            this.applySession(response);
          }
        })
      )
    );
  }

  /**
   * Starts the startup refresh at most once for this service lifetime. Its expected
   * anonymous result is deliberately settled locally instead of redirecting public
   * routes to login.
   */
  bootstrap(): Observable<void> {
    if (this.bootstrapRequest === null) {
      this.bootstrapRequest = this.refreshSession().pipe(
        map(() => undefined),
        catchError(() => {
          this.clearSession();
          return of(undefined);
        }),
        finalize(() => {
          this.sessionStateValue.set(this.isAuthenticated() ? 'auth' : 'anon');
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.bootstrapRequest;
  }

  /**
   * Reuses a single in-flight same-origin refresh. The functional interceptor owns
   * retrying original API requests; this method only obtains the new session.
   */
  refreshSession(): Observable<SessionResponse> {
    if (this.refreshRequest === null) {
      const refreshEpoch = this.sessionEpoch;
      this.refreshRequest = this.http.post<SessionResponse>('/auth/refresh', {}).pipe(
        tap((response) => {
          if (refreshEpoch === this.sessionEpoch) {
            this.applySession(response);
          }
        }),
        finalize(() => {
          this.refreshRequest = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.refreshRequest;
  }

  /**
   * Logout is best-effort because the local access JWT must be discarded before a
   * cold start or a hanging network request can delay the server-side revocation.
   */
  logout(): Observable<void> {
    this.sessionEpoch += 1;
    this.clearSession();
    return this.http.post<void>('/auth/logout', {}).pipe(catchError(() => of(undefined)));
  }

  /** Clears the memory-only access state without touching browser storage. */
  clearSession(): void {
    const previousUser = this.currentUserState();
    this.accessTokenState.set(null);
    this.currentUserState.set(null);
    this.sessionStateValue.set('anon');
    if (previousUser !== null) {
      this.sessionChangeValue.set({ previousUserId: previousUser.id, currentUser: null });
    }
  }

  /**
   * Returns true only for the first failed auto-refresh after an authenticated request.
   * The interceptor uses it to make exactly one login navigation for a failed queue.
   */
  handleAutoRefreshFailure(): boolean {
    this.clearSession();
    if (this.refreshFailureRedirected) {
      return false;
    }

    this.refreshFailureRedirected = true;
    return true;
  }

  /** Captures the current in-memory session generation for an in-flight API request. */
  getSessionEpoch(): number {
    return this.sessionEpoch;
  }

  /** True only while no logout or newer login has superseded the captured session. */
  isSessionCurrent(epoch: number): boolean {
    return epoch === this.sessionEpoch;
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

  private applySession(response: SessionResponse): void {
    const previousUser = this.currentUserState();
    this.accessTokenState.set(response.accessToken);
    this.currentUserState.set(response.user);
    this.sessionStateValue.set('auth');
    this.refreshFailureRedirected = false;

    if (previousUser?.id !== response.user.id) {
      this.sessionChangeValue.set({
        previousUserId: previousUser?.id ?? null,
        currentUser: response.user
      });
    }
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
