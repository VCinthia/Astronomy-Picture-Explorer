import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map } from 'rxjs';

import { AuthService } from '../services/auth.service';

/** Waits for the root session bootstrap; it never performs HTTP work itself. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.bootstrap().pipe(
    map(() => auth.isAuthenticated() || router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    }))
  );
};
