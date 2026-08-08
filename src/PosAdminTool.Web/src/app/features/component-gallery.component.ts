import { Component } from '@angular/core';
import { StatusMarkerComponent } from '../shared/status-marker.component';
import { SignalStatus } from '../shared/ui-fixtures';

@Component({ selector: 'app-component-gallery', imports: [StatusMarkerComponent], template: `
  <section class="route-heading"><span class="eyebrow">DEVELOPMENT ONLY</span><h1>Component gallery</h1><p>Visual and semantic reference for the shared DBS primitives.</p></section>
  <section class="instrument-panel"><h2>Status markers</h2><div class="gallery-row">@for (status of statuses; track status) { <span><app-status-marker [status]="status" [label]="status"></app-status-marker>{{ status }}</span> }</div></section>
  <section class="instrument-panel"><h2>Form and feedback</h2><label>Branch label<input value="P087" aria-label="Branch label" /></label><div class="skeleton" aria-label="Loading content"></div><p class="error-state" role="alert">Connection evidence could not be refreshed. Check local Agent status.</p></section>
`, })
export class ComponentGalleryComponent { protected readonly statuses: SignalStatus[] = ['loading', 'ready', 'degraded', 'unreachable', 'stale', 'unknown']; }
