import { Component, OnInit, inject, signal } from '@angular/core';
import { AgentApi, Capability, Connectivity, Identity } from '../core/agent-api.service';

@Component({ standalone: true, template: `
  <section class="route-heading"><span class="eyebrow">AGENT EVIDENCE</span><h1>Device</h1><p>Read-only device identity and independently checked connection evidence.</p></section>
  @if (identity(); as item) { <section class="workspace-grid"><article class="instrument-panel"><span class="eyebrow">BRANCH IDENTITY</span><h2>{{ item.branchCode || 'Unassigned' }} · POS {{ item.posNumber || '—' }}</h2><p>Release {{ item.release || 'Unknown' }} · {{ item.clientName || 'Client unknown' }}</p></article>
  @if (connectivity(); as status) { <article class="instrument-panel"><span class="eyebrow">LOCAL SQL</span><h2>{{ status.localSql.freshness }}</h2><p>{{ status.localSql.detail }}</p><time class="mono">{{ status.localSql.lastCheckedUtc || 'Not checked' }}</time></article><article class="instrument-panel"><span class="eyebrow">MAIN SERVER</span><h2>{{ status.mainServer.freshness }}</h2><p>{{ status.mainServer.detail }}</p><time class="mono">{{ status.mainServer.lastCheckedUtc || 'Not checked' }}</time></article> }</section> }
  @if (capability(); as item) { <section class="instrument-panel"><span class="eyebrow">AGENT CAPABILITIES</span><h2>Agent {{ item.agentVersion }}</h2><p>{{ item.operatingSystem }}</p><p class="mono">Browse roots: {{ roots(item) }}</p></section> }
`, })
export class DevicePageComponent implements OnInit {
  private readonly api = inject(AgentApi); protected readonly identity = signal<Identity | null>(null); protected readonly connectivity = signal<Connectivity | null>(null); protected readonly capability = signal<Capability | null>(null);
  async ngOnInit(): Promise<void> { try { const [identity, connectivity, capability] = await Promise.all([this.api.get<Identity>('/device/identity'), this.api.get<Connectivity>('/device/connectivity'), this.api.get<Capability>('/device/capabilities')]); this.identity.set(identity); this.connectivity.set(connectivity); this.capability.set(capability); } catch { /* Device diagnostics are best-effort. */ } }
  protected roots(value: Capability): string { return value.browseRoots.length ? value.browseRoots.map((root) => root.displayName).join(', ') : 'None configured'; }
}
