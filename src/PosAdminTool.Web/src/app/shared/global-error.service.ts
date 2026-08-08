import { ErrorHandler, Injectable, inject, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class GlobalErrorService {
  readonly message = signal<string | null>(null);
  showSafeMessage(): void { this.message.set('This route could not be displayed safely. Return to Overview or retry the diagnostic area.'); }
  dismiss(): void { this.message.set(null); }
}

@Injectable()
export class AppErrorHandler implements ErrorHandler {
  private readonly errors = inject(GlobalErrorService);
  handleError(error: unknown): void { void error; this.errors.showSafeMessage(); }
}
