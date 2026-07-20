import { routes } from './app.routes';

describe('account routes', () => {
  it('redirects the legacy root route to canonical lazy /home', () => {
    const root = routes.find((candidate) => candidate.path === '');
    const home = routes.find((candidate) => candidate.path === 'home');

    expect(root?.redirectTo).toBe('home');
    expect(root?.pathMatch).toBe('full');
    expect(home?.loadComponent).toBeDefined();
  });

  it('keeps register, login, and confirmation as public lazy routes', () => {
    for (const path of ['register', 'login', 'confirm-email']) {
      const route = routes.find((candidate) => candidate.path === path);

      expect(route).toBeDefined();
      expect(route?.component).toBeUndefined();
      expect(route?.loadComponent).toBeDefined();
      expect(route?.canActivate).toBeUndefined();
    }
  });

  it('protects the existing lazy favorites route with the session guard', () => {
    const favorites = routes.find((candidate) => candidate.path === 'favorites');

    expect(favorites?.loadComponent).toBeDefined();
    expect(favorites?.canActivate?.length).toBe(1);
  });
});
