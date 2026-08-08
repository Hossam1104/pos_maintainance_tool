import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SignalNode } from './ui-fixtures';
import { StatusMarkerComponent } from './status-marker.component';

@Component({ selector: 'app-signal-path', imports: [RouterLink, StatusMarkerComponent], template: `
  <section class="signal-path" [class.compact]="compact()" aria-label="Branch signal path">
    @for (node of nodes(); track node.label; let last = $last) {
      <a class="signal-node" [routerLink]="node.route" [attr.aria-label]="node.label + ': ' + node.status + '. ' + node.evidence + '. Last checked ' + node.checkedAt">
        <span class="node-head"><app-status-marker [status]="node.status" [label]="node.status"></app-status-marker><strong>{{ node.label }}</strong></span>
        <span class="status-word">{{ node.status }}</span><span class="node-evidence">{{ node.evidence }}</span><time class="mono">{{ node.checkedAt }}</time>
      </a>
      @if (!last) { <span class="signal-rail" aria-hidden="true"></span> }
    }
  </section>`,
})
export class SignalPathComponent { readonly nodes = input.required<readonly SignalNode[]>(); readonly compact = input(false); }
