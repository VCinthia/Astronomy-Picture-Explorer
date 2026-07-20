import { routes } from './app.routes';

describe('account routes', () => {
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
