import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SignalPathComponent } from '../shared/signal-path.component';
import { branchSignalFixture } from '../shared/ui-fixtures';
import { AgentApi, Connectivity, Identity, Operation } from '../core/agent-api.service';

@Component({ selector: 'app-overview-page', imports: [RouterLink, SignalPathComponent], template: `
  <section class="route-heading"><span class="eyebrow">BRANCH SIGNAL DESK</span><h1>Overview</h1><p>Read the branch path first. Work from evidence, not assumed availability.</p></section>
  <section class="instrument-panel signal-thesis"><div class="panel-heading"><div><span class="eyebrow">LIVE PATH</span><h2>Branch signal</h2></div><span class="mono">EVIDENCE IN UTC</span></div><app-signal-path [nodes]="nodes()" /></section>
  <section class="workspace-grid"><article class="instrument-panel"><span class="eyebrow">RECOMMENDED ACTION</span><h2>{{ recommendation() }}</h2><p>Use the latest evidence before taking a host action.</p><a routerLink="/device" class="primary-action">Open device diagnostics</a></article><article class="instrument-panel"><span class="eyebrow">ACTIVE OPERATION</span>@if (operation(); as active) { <h2>{{ active.operationType }}</h2><p class="mono">{{ active.state }} · {{ active.progressPercent }}%</p> } @else { <h2>No work in progress</h2><p class="mono">NO ACTIVE AGENT OPERATION</p> }</article><article class="empty-state"><span aria-hidden="true">◇</span><h2>No maintenance has run on this device yet</h2><p>Connection evidence remains available when the Agent returns.</p><a routerLink="/device">View diagnostics</a></article></section>
`, })
export class OverviewPageComponent implements OnInit {
  private readonly api = inject(AgentApi);
  protected readonly nodes = signal(branchSignalFixture);
  protected readonly operation = signal<Operation | null>(null);
  protected readonly recommendation = signal('Inspect device diagnostics');
  async ngOnInit(): Promise<void> {
    try {
      const [identity, connectivity, operations] = await Promise.all([this.api.get<Identity>('/device/identity'), this.api.get<Connectivity>('/device/connectivity'), this.api.get<Operation[]>('/operations')]);
      this.nodes.set(toNodes(identity, connectivity));
      this.operation.set(operations.find((item) => item.state === 'running' || item.state === 'queued') ?? null);
      if (connectivity.localSql.freshness !== 'fresh') this.recommendation.set('Test the local SQL connection');
      else if (connectivity.mainServer.freshness !== 'fresh') this.recommendation.set('Inspect main-server TCP reachability');
    } catch { /* Keep the explicitly-labelled shell fallback when the Agent is unavailable. */ }
  }
}
function toNodes(identity: Identity, connectivity: Connectivity) {
  return [
    { label: 'This device', status: identity.branchCode ? 'ready' : 'unknown', evidence: identity.branchCode ? `${identity.branchCode} / POS ${identity.posNumber || 'unassigned'} identity confirmed` : 'Device identity has not been configured', checkedAt: new Date().toISOString(), route: '/device' },
    { label: 'Local SQL', status: signalStatus(connectivity.localSql), evidence: connectivity.localSql.detail, checkedAt: connectivity.localSql.lastCheckedUtc ?? 'Not checked', route: '/device' },
    { label: 'Main server', status: signalStatus(connectivity.mainServer), evidence: connectivity.mainServer.detail, checkedAt: connectivity.mainServer.lastCheckedUtc ?? 'Not checked', route: '/device' },
  ] as const;
}
export function signalStatus(evidence: { freshness: string; detail: string }): 'ready' | 'degraded' | 'unreachable' | 'unknown' { if (evidence.freshness === 'unknown') return 'unknown'; return evidence.freshness === 'fresh' ? 'ready' : evidence.detail.includes('unreachable') ? 'unreachable' : 'degraded'; }
