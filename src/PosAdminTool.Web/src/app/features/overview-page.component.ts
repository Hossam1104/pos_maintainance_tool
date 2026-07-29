import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SignalPathComponent } from '../shared/signal-path.component';
import { branchSignalFixture } from '../shared/ui-fixtures';

@Component({ selector: 'app-overview-page', imports: [RouterLink, SignalPathComponent], template: `
  <section class="route-heading"><span class="eyebrow">BRANCH SIGNAL DESK</span><h1>Overview</h1><p>Read the branch path first. Work from evidence, not assumed availability.</p></section>
  <section class="instrument-panel signal-thesis"><div class="panel-heading"><div><span class="eyebrow">LIVE PATH</span><h2>Branch signal</h2></div><span class="mono">CHECK 14:32:08 UTC</span></div><app-signal-path [nodes]="nodes" /></section>
  <section class="workspace-grid"><article class="instrument-panel"><span class="eyebrow">RECOMMENDED ACTION</span><h2>Inspect RMS service state</h2><p>One required RMS service is not running. Review the last check before taking a host action.</p><a routerLink="/services" class="primary-action">Open service diagnostics</a></article><article class="instrument-panel"><span class="eyebrow">ACTIVE OPERATION</span><h2>No work in progress</h2><p class="mono">AGENT UNREACHABLE · MUTATIONS DISABLED</p></article><article class="empty-state"><span aria-hidden="true">◇</span><h2>No maintenance has run on this device yet</h2><p>Connection evidence will remain available when the Agent returns.</p><a routerLink="/device">View diagnostics</a></article></section>
`, })
export class OverviewPageComponent { protected readonly nodes = branchSignalFixture; }
