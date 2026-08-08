import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { AgentApi, ArtifactMetadata, BackupOptions, BrowseResult, Capability, OperationDetail, Operation } from '../core/agent-api.service';

type BackupStep = 1 | 2 | 3 | 4;

@Component({
  selector: 'app-backups-page',
  standalone: true,
  templateUrl: './backups-page.component.html',
})
export class BackupsPageComponent implements OnInit, OnDestroy {
  protected readonly api = inject(AgentApi);
  protected readonly steps = [
    { number: 1, label: 'Select' },
    { number: 2, label: 'Review' },
    { number: 3, label: 'Run' },
    { number: 4, label: 'Result' },
  ] as const;
  protected readonly step = signal<BackupStep>(1);
  protected readonly options = signal<BackupOptions | null>(null);
  protected readonly capability = signal<Capability | null>(null);
  protected readonly browseResult = signal<BrowseResult | null>(null);
  protected readonly destinationHandle = signal<string | null>(null);
  protected readonly destinationReferenceValue = signal<string | null>(null);
  protected readonly selectedIds = signal<string[]>([]);
  protected readonly operation = signal<OperationDetail | null>(null);
  protected readonly artifacts = signal<ArtifactMetadata[]>([]);
  protected readonly catalog = signal<ArtifactMetadata[]>([]);
  protected readonly loading = signal(false);
  protected readonly message = signal('');
  private timer: number | null = null;
  private events: EventSource | null = null;

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  ngOnDestroy(): void {
    this.events?.close();
    if (this.timer !== null) window.clearTimeout(this.timer);
  }

  protected toggle(componentId: string): void {
    this.selectedIds.update(items => items.includes(componentId) ? items.filter(item => item !== componentId) : [...items, componentId]);
  }

  protected selectAll(): void {
    this.selectedIds.set(this.options()?.components.map(component => component.componentId) ?? []);
  }

  protected isSelected(componentId: string): boolean {
    return this.selectedIds().includes(componentId);
  }

  protected selectedComponents(): { componentId: string; displayName: string }[] {
    const selected = new Set(this.selectedIds());
    return this.options()?.components.filter(component => selected.has(component.componentId)) ?? [];
  }

  protected async openFolder(rootId: string, relativeSubPath: string): Promise<void> {
    try {
      this.browseResult.set(await this.api.mutate<BrowseResult>('post', '/files/browse', { rootId, relativeSubPath }));
      this.destinationHandle.set(null);
      this.destinationReferenceValue.set(null);
      this.message.set('');
    } catch {
      this.message.set('The managed destination could not be read.');
    }
  }

  protected async chooseFolder(browser: BrowseResult): Promise<void> {
    try {
      const handle = await this.api.mutate<{ handleId: string }>('post', '/files/handles', { rootId: browser.rootId, relativeSubPath: browser.relativeSubPath, purpose: 'backupDestination' });
      this.destinationHandle.set(handle.handleId);
      this.destinationReferenceValue.set(this.destinationReference(browser));
      this.message.set('Destination selected. Review the package before starting it.');
    } catch {
      this.message.set('The Agent did not issue a destination handle.');
    }
  }

  protected destinationReference(browser: BrowseResult): string {
    return browser.relativeSubPath ? browser.rootId + ' / ' + browser.relativeSubPath : browser.rootId;
  }

  protected parent(path: string): string {
    const last = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
    return last < 0 ? '' : path.slice(0, last);
  }

  protected canReview(): boolean {
    return this.selectedIds().length > 0 && this.destinationHandle() !== null && this.options() !== null;
  }

  protected review(): void {
    if (!this.canReview()) {
      this.message.set('Select at least one component and a managed destination first.');
      return;
    }
    this.message.set('');
    this.step.set(2);
  }

  protected async run(): Promise<void> {
    const handle = this.destinationHandle();
    if (!handle || !this.canReview()) return;
    try {
      const detail = await this.api.mutate<OperationDetail>('post', '/backups', { componentIds: this.selectedIds(), destinationHandle: handle, idempotencyKey: crypto.randomUUID() });
      this.operation.set(detail);
      this.step.set(3);
      this.connectEvents();
      await this.poll();
    } catch {
      this.message.set('The backup was not accepted. Review the destination and preflight evidence.');
    }
  }

  protected async cancel(): Promise<void> {
    const current = this.operation();
    if (!current || !this.isActive(current.state)) return;
    try {
      this.operation.set(await this.api.mutate<OperationDetail>('post', '/operations/' + current.operationId + '/cancel'));
      await this.poll();
    } catch {
      this.message.set('The Agent could not cancel the backup.');
    }
  }

  protected isActive(state: string): boolean {
    return state === 'queued' || state === 'running';
  }

  protected statusLabel(state: string): string {
    return state.replace(/([A-Z])/g, ' $1').replace(/^./, character => character.toUpperCase());
  }

  protected async copyDestination(value: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(value);
      this.message.set('Destination reference copied.');
    } catch {
      this.message.set('Copy was unavailable; select the reference manually.');
    }
  }

  protected formatBytes(value: number): string {
    if (value < 1024) return value + ' B';
    if (value < 1024 * 1024) return (value / 1024).toFixed(1) + ' KB';
    if (value < 1024 * 1024 * 1024) return (value / (1024 * 1024)).toFixed(1) + ' MB';
    return (value / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
  }

  protected reset(): void {
    this.step.set(1);
    this.operation.set(null);
    this.artifacts.set([]);
    this.destinationHandle.set(null);
    this.destinationReferenceValue.set(null);
  }

  protected async loadCatalog(): Promise<void> {
    try {
      this.catalog.set(await this.api.get<ArtifactMetadata[]>('/backups'));
    } catch {
      this.message.set('The artifact catalog is unavailable.');
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [options, capability, catalog, operations] = await Promise.all([
        this.api.get<BackupOptions>('/backups/options'),
        this.api.get<Capability>('/device/capabilities'),
        this.api.get<ArtifactMetadata[]>('/backups'),
        this.api.get<Operation[]>('/operations'),
      ]);
      this.options.set(options);
      this.capability.set(capability);
      this.catalog.set(catalog);
      const active = operations.find(item => item.operationType === 'backup' && this.isActive(item.state));
      if (active) {
        this.step.set(3);
        await this.rehydrate(active.operationId);
      }
    } catch {
      this.message.set('Backup options are unavailable. Check the Agent connection and try again.');
    } finally {
      this.loading.set(false);
    }
  }

  private async rehydrate(operationId: string): Promise<void> {
    try {
      const detail = await this.api.get<OperationDetail>('/operations/' + operationId);
      this.operation.set(detail);
      this.connectEvents();
      await this.poll();
    } catch {
      this.message.set('The backup operation could not be rehydrated.');
    }
  }

  private async poll(): Promise<void> {
    const current = this.operation();
    if (!current) return;
    try {
      const detail = await this.api.get<OperationDetail>('/operations/' + current.operationId);
      this.operation.set(detail);
      if (this.isActive(detail.state)) {
        this.timer = window.setTimeout(() => void this.poll(), 350);
      } else {
        await this.finish(detail);
      }
    } catch {
      this.timer = window.setTimeout(() => void this.poll(), 1000);
    }
  }

  private async finish(detail: OperationDetail): Promise<void> {
    this.events?.close();
    this.step.set(4);
    try {
      this.artifacts.set(await Promise.all(detail.resultArtifactIds.map(id => this.api.get<ArtifactMetadata>('/artifacts/' + id))));
      await this.loadCatalog();
    } catch {
      this.message.set('The operation finished, but artifact metadata could not be loaded.');
    }
  }

  private connectEvents(): void {
    this.events?.close();
    this.events = new EventSource('/api/v1/events');
    this.events.addEventListener('operation', event => {
      const detail = JSON.parse((event as MessageEvent<string>).data) as OperationDetail;
      if (detail.operationId === this.operation()?.operationId) {
        this.operation.set(detail);
      }
    });
    this.events.onerror = () => {
      this.events?.close();
      this.events = null;
    };
  }
}
