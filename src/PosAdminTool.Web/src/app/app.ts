import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DOCUMENT } from '@angular/common';
import { SignalPathComponent } from './shared/signal-path.component';
import { branchSignalFixture, navigationItems } from './shared/ui-fixtures';
import { GlobalErrorService } from './shared/global-error.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, SignalPathComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly document = inject(DOCUMENT);
  protected readonly errors = inject(GlobalErrorService);
  protected readonly navItems = navigationItems;
  protected readonly signalNodes = branchSignalFixture;
  protected readonly activityOpen = signal(true);
  protected readonly dark = signal(false);
  protected readonly activeThemeLabel = computed(() => (this.dark() ? 'Dark theme' : 'Light theme'));

  protected toggleTheme(): void {
    this.dark.update((value) => !value);
    this.document.documentElement.dataset['theme'] = this.dark() ? 'dark' : 'light';
  }
}
