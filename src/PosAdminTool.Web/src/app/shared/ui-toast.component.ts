import { Component, input } from '@angular/core';

@Component({ selector: 'app-ui-toast', template: `@if (message()) { <div class="toast" role="status" aria-live="polite">{{ message() }}</div> }` })
export class UiToastComponent { readonly message = input(''); }
