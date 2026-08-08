import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({ selector: 'app-placeholder-page', imports: [RouterLink], template: `
  <section class="route-heading"><span class="eyebrow">AGENT WORKSPACE</span><h1>{{ title }}</h1><p>{{ detail }}</p></section>
  <section class="instrument-panel placeholder"><span class="marker marker-unknown"></span><div><h2>{{ title }} is unavailable while the Agent is unreachable</h2><p>Host changes are deliberately disabled. Review the device evidence or return when the local Agent is available.</p><a routerLink="/device" class="primary-action">View device evidence</a></div></section>
`, })
export class PlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);
  protected readonly title = String(this.route.snapshot.data['title'] ?? 'Workspace');
  protected readonly detail = String(this.route.snapshot.data['detail'] ?? 'This route is ready for its Agent-backed workflow.');
}
