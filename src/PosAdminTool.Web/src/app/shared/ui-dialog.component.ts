import { Component, input, output } from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';

@Component({
  selector: 'app-ui-dialog',
  imports: [CdkTrapFocus],
  template: `@if (open()) { <div class="dialog-backdrop" role="presentation"><section class="dbs-dialog" cdkTrapFocus cdkTrapFocusAutoCapture role="dialog" aria-modal="true" [attr.aria-labelledby]="titleId"><header><h2 [id]="titleId">{{ title() }}</h2><button type="button" (click)="dismiss.emit()" aria-label="Close dialog">×</button></header><p>{{ description() }}</p><footer><button cdkFocusRegionStart type="button" class="primary-action" (click)="confirm.emit()">{{ confirmLabel() }}</button><button type="button" (click)="dismiss.emit()">Cancel</button></footer></section></div> }`,
})
export class UiDialogComponent {
  readonly open = input(false); readonly title = input('Confirm action'); readonly description = input('Review this action before continuing.'); readonly confirmLabel = input('Continue');
  readonly dismiss = output<void>(); readonly confirm = output<void>();
  protected readonly titleId = 'dbs-dialog-title';
}
