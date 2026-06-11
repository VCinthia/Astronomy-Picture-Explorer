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
  { path: '**', redirectTo: '' }
];
