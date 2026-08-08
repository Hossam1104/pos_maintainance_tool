import { Component, input } from '@angular/core';
import { SignalStatus } from './ui-fixtures';

@Component({ selector: 'app-status-marker', template: '<span class="marker" [class]="\'marker marker-\' + status()" aria-hidden="true"></span><span class="sr-only">{{ label() }}</span>' })
export class StatusMarkerComponent {
  readonly status = input.required<SignalStatus>();
  readonly label = input.required<string>();
}
