import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './services/auth.service';
import { AUTHOR_NAME, AUTHOR_SITE_URL, SITE_CREATED } from './config/site.config';
import { BottomNavComponent } from './components/bottom-nav/bottom-nav.component';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, BottomNavComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  readonly auth = inject(AuthService);

  readonly authorName = AUTHOR_NAME;
  readonly authorSiteUrl = AUTHOR_SITE_URL;
  readonly siteCreated = SITE_CREATED;

  logout(): void {
    this.auth.logout().subscribe();
  }
}
