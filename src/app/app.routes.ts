import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Astronomy Picture Explorer',
    loadComponent: () => import('./pages/home/home.component').then((m) => m.HomeComponent)
  },
  {
    path: 'explorer',
    title: 'Explore by date · Astronomy Picture Explorer',
    loadComponent: () =>
      import('./pages/explorer/explorer.component').then((m) => m.ExplorerComponent)
  },
  {
    path: 'favorites',
    title: 'Favorites · Astronomy Picture Explorer',
    loadComponent: () =>
      import('./pages/favorites/favorites.component').then((m) => m.FavoritesComponent)
  },
  {
    path: 'register',
    title: 'Create account · Astronomy Picture Explorer',
    loadComponent: () =>
      import('./auth/register/register.component').then((m) => m.RegisterComponent)
  },
  {
    path: 'login',
    title: 'Sign in · Astronomy Picture Explorer',
    loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'confirm-email',
    title: 'Confirm email · Astronomy Picture Explorer',
    loadComponent: () =>
      import('./auth/confirm-email/confirm-email.component').then((m) => m.ConfirmEmailComponent)
  },
  { path: '**', redirectTo: '' }
];
