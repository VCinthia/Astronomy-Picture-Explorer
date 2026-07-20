import {
  HttpContextToken,
  HttpErrorResponse,
  HttpInterceptorFn
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';

/** Internal metadata only: HttpContext never serializes into request headers. */
const RETRIED_AFTER_REFRESH = new HttpContextToken<boolean>(() => false);

/**
 * Adds a memory-only Bearer token exclusively to application API requests. A failed
 * token refresh is shared by AuthService and each original request gets one retry.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (
    !isProtectedApiRequest(request.url) ||
    isAuthRequest(request.url) ||
    request.context.get(RETRIED_AFTER_REFRESH)
  ) {
    return next(request);
  }

  const token = auth.accessToken();
  if (token === null) {
    return next(request);
  }
  const requestSessionEpoch = auth.getSessionEpoch();

  const authorizedRequest = request.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      if (!auth.isSessionCurrent(requestSessionEpoch)) {
        return throwError(() => error);
      }

      return auth.refreshSession().pipe(
        catchError((refreshError: unknown) => {
          if (auth.isSessionCurrent(requestSessionEpoch)) {
            redirectToLoginOnce(auth, router);
          }
          return throwError(() => refreshError);
        }),
        switchMap(() => {
          if (!auth.isSessionCurrent(requestSessionEpoch)) {
            return throwError(() => error);
          }
          const refreshedToken = auth.accessToken();
          if (refreshedToken === null) {
            return throwError(() => error);
          }

          return next(authorizedRequest.clone({
            context: authorizedRequest.context.set(RETRIED_AFTER_REFRESH, true),
            setHeaders: { Authorization: `Bearer ${refreshedToken}` }
          }));
        })
      );
    })
  );
};

function isProtectedApiRequest(url: string): boolean {
  return url.startsWith('/api/');
}

function isAuthRequest(url: string): boolean {
  return url.startsWith('/auth/');
}

function redirectToLoginOnce(auth: AuthService, router: Router): void {
  if (!auth.handleAutoRefreshFailure() || router.url.startsWith('/login')) {
    return;
  }

  const returnUrl = isSafeReturnUrl(router.url) ? router.url : '/';
  void router.navigate(['/login'], { queryParams: { returnUrl } });
}

function isSafeReturnUrl(value: string): boolean {
  return value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/auth/');
}
