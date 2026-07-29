import { Component, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AgentApi, BrowseResult, Capability, Config } from '../core/agent-api.service';

@Component({ standalone: true, imports: [ReactiveFormsModule], template: `
  <section class="route-heading"><span class="eyebrow">AGENT-OWNED CONFIGURATION</span><h1>Settings</h1><p>Passwords are write-only: blank keeps a retained secret; Clear removes it explicitly. Managed paths stay on the Agent.</p></section>
  @if (message(); as notice) { <p class="status-message" role="status">{{ notice }}</p> }
  <form class="settings-form instrument-panel" [formGroup]="form" (ngSubmit)="save()">
    <fieldset><legend>Branch identity</legend><label>Branch code <input formControlName="branchCode" maxlength="50" required></label><label>POS number <input formControlName="posNumber" maxlength="50"></label><label>Release <input formControlName="release" maxlength="100"></label><label>Client name <input formControlName="clientName" maxlength="100"></label></fieldset>
    <fieldset><legend>SQL connection</legend><label>SQL instance <input formControlName="sqlInstance" maxlength="200"></label><label>SQL user <input formControlName="sqlUser" maxlength="200"></label><label>SQL password <small>{{ config()?.hasSqlPassword ? 'A password is stored. Enter a value only to replace it.' : 'No password is stored.' }}</small><input type="password" formControlName="sqlPassword" autocomplete="new-password"></label><button type="button" (click)="clear('sqlPassword')" [disabled]="!config()?.hasSqlPassword">Clear SQL password</button></fieldset>
    <fieldset><legend>Main server</legend><label>Server URL <input formControlName="apiBaseUrl" placeholder="https://server" maxlength="500"></label><label>Databases <small>One per line</small><textarea formControlName="databases" rows="4"></textarea></label><label>Services <small>One per line</small><textarea formControlName="services" rows="4"></textarea></label></fieldset>
    <fieldset><legend>Downloader</legend><label>API URL <input formControlName="rdbApiUrl" placeholder="https://server" maxlength="500"></label><label>RDB server <input formControlName="rdbServerIp" maxlength="200"></label><label>RDB user <input formControlName="rdbUsername" maxlength="200"></label><label>RDB password <small>{{ config()?.downloader?.hasRdbPassword ? 'A password is stored. Enter a value only to replace it.' : 'No password is stored.' }}</small><input type="password" formControlName="rdbPassword" autocomplete="new-password"></label><button type="button" (click)="clear('rdbPassword')" [disabled]="!config()?.downloader?.hasRdbPassword">Clear RDB password</button><label>Known branch codes <small>One per line</small><textarea formControlName="knownBranchCodes" rows="3"></textarea></label><label>Poll interval (seconds) <input type="number" formControlName="pollIntervalSeconds" min="1" max="3600"></label><label>Timeout (seconds) <input type="number" formControlName="timeoutSeconds" min="1" max="86400"></label></fieldset>
    <div class="form-actions"><button class="primary-action" type="submit" [disabled]="form.invalid || saving()">Save settings</button><button type="button" (click)="importRms()">Import RMS+</button><button type="button" (click)="testDatabase()">Test database</button><button type="button" (click)="verifyBranch()">Verify branch</button></div>
  </form>
  <section class="instrument-panel browse-panel"><div class="panel-heading"><div><span class="eyebrow">MANAGED PATHS</span><h2>Approved browse roots</h2></div><span class="mono">ROOT IDS ONLY</span></div><p>Roots are configured on the Agent. This browser never accepts or reveals an absolute path.</p>
    @if (capability(); as item) { @if (item.browseRoots.length) { <div class="browse-roots">@for (root of item.browseRoots; track root.rootId) { <button type="button" (click)="browse(root.rootId, '')">Browse {{ root.displayName }}</button> }</div> } @else { <p>No browse roots are configured on this Agent.</p> } }
    @if (browseResult(); as result) { <div class="browse-result"><p class="mono">{{ result.rootId }}{{ result.relativeSubPath ? ' / ' + result.relativeSubPath : '' }}</p>@if (result.relativeSubPath) { <button type="button" (click)="browse(result.rootId, parent(result.relativeSubPath))">Up one level</button> }<ul>@for (entry of result.entries; track entry.relativeSubPath) { <li>@if (entry.isDirectory) { <button type="button" (click)="browse(result.rootId, entry.relativeSubPath)">{{ entry.name }}</button> } @else { <span>{{ entry.name }}</span> } <small>{{ entry.isDirectory ? 'Folder' : 'File' }}</small></li> }</ul></div> }
  </section>
`, })
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(AgentApi); private readonly fb = inject(FormBuilder);
  protected readonly config = signal<Config | null>(null); protected readonly capability = signal<Capability | null>(null); protected readonly browseResult = signal<BrowseResult | null>(null); protected readonly message = signal(''); protected readonly saving = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    branchCode: ['', [Validators.required, Validators.maxLength(50)]], posNumber: ['', Validators.maxLength(50)], release: ['', Validators.maxLength(100)], clientName: ['', Validators.maxLength(100)],
    sqlInstance: ['', Validators.maxLength(200)], sqlUser: ['', Validators.maxLength(200)], sqlPassword: [''], apiBaseUrl: ['', [Validators.maxLength(500), Validators.pattern(/^$|https?:\/\/.+/)]], databases: ['', Validators.maxLength(5000)], services: ['', Validators.maxLength(5000)],
    rdbApiUrl: ['', [Validators.maxLength(500), Validators.pattern(/^$|https?:\/\/.+/)]], rdbServerIp: ['', Validators.maxLength(200)], rdbUsername: ['', Validators.maxLength(200)], rdbPassword: [''], knownBranchCodes: ['', Validators.maxLength(5000)], pollIntervalSeconds: [5, [Validators.min(1), Validators.max(3600)]], timeoutSeconds: [1800, [Validators.min(1), Validators.max(86400)]],
  });

  async ngOnInit(): Promise<void> { await this.load(); }
  async save(): Promise<void> {
    const current = this.config(); if (!current || this.form.invalid) return;
    this.saving.set(true); this.message.set(''); const value = this.form.getRawValue();
    try {
      const updated = await this.api.mutate<Config>('put', '/configuration', {
        sqlInstance: value.sqlInstance, sqlUser: value.sqlUser, sqlPassword: value.sqlPassword || null, branchCode: value.branchCode, posNumber: value.posNumber, release: value.release, clientName: value.clientName, apiBaseUrl: value.apiBaseUrl,
        databases: lines(value.databases), services: lines(value.services), expectedVersion: current.version,
        downloader: { apiUrl: value.rdbApiUrl, rdbServerIp: value.rdbServerIp, rdbUsername: value.rdbUsername, rdbPassword: value.rdbPassword || null, knownBranchCodes: lines(value.knownBranchCodes), pollIntervalSeconds: value.pollIntervalSeconds, timeoutSeconds: value.timeoutSeconds },
      });
      this.config.set(updated); this.patch(); this.message.set('Settings saved.');
    } catch (error: unknown) {
      this.message.set(isConflict(error) ? 'Settings changed elsewhere. Your non-secret edits are still in this form; reload only when ready.' : 'Settings could not be saved.');
    } finally { this.clearSecretInputs(); this.saving.set(false); }
  }
  async clear(secret: 'sqlPassword' | 'rdbPassword'): Promise<void> {
    const current = this.config(); if (!current) return;
    try { const updated = await this.api.mutate<Config>('post', '/configuration/secrets/clear', { secret, expectedVersion: current.version }); this.config.set(updated); this.clearSecretInputs(); this.message.set(`${secret === 'sqlPassword' ? 'SQL' : 'RDB'} password cleared.`); } catch (error: unknown) { this.message.set(isConflict(error) ? 'Settings changed elsewhere. Reload before clearing a password.' : 'Password could not be cleared.'); }
  }
  async importRms(): Promise<void> {
    if (this.form.dirty && !window.confirm('Import will replace the current non-secret form values. Continue?')) return;
    try { this.config.set(await this.api.mutate<Config>('post', '/configuration/import-rms')); this.patch(); this.clearSecretInputs(); this.message.set('RMS configuration imported.'); } catch { this.message.set('RMS import failed.'); }
  }
  async testDatabase(): Promise<void> { await this.run('/configuration/test-database'); }
  async verifyBranch(): Promise<void> { await this.run('/configuration/verify-branch'); }
  async browse(rootId: string, relativeSubPath: string): Promise<void> { try { this.browseResult.set(await this.api.mutate<BrowseResult>('post', '/files/browse', { rootId, relativeSubPath })); } catch { this.message.set('The selected browse root could not be read.'); } }
  protected parent(path: string): string { const last = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\')); return last < 0 ? '' : path.slice(0, last); }
  private async load(): Promise<void> { try { const [config, capability] = await Promise.all([this.api.get<Config>('/configuration'), this.api.get<Capability>('/device/capabilities')]); this.config.set(config); this.capability.set(capability); this.patch(); } catch { this.message.set('Agent configuration is unavailable.'); } }
  private patch(): void { const c = this.config(); if (!c) return; this.form.patchValue({ branchCode: c.branchCode, posNumber: c.posNumber, release: c.release, clientName: c.clientName, sqlInstance: c.sqlInstance, sqlUser: c.sqlUser, sqlPassword: '', apiBaseUrl: c.apiBaseUrl, databases: c.databases.join('\n'), services: c.services.join('\n'), rdbApiUrl: c.downloader.apiUrl, rdbServerIp: c.downloader.rdbServerIp, rdbUsername: c.downloader.rdbUsername, rdbPassword: '', knownBranchCodes: c.downloader.knownBranchCodes.join('\n'), pollIntervalSeconds: c.downloader.pollIntervalSeconds, timeoutSeconds: c.downloader.timeoutSeconds }); this.form.markAsPristine(); }
  private clearSecretInputs(): void { this.form.patchValue({ sqlPassword: '', rdbPassword: '' }, { emitEvent: false }); }
  private async run(path: string): Promise<void> { try { const result = await this.api.mutate<{ evidence: { detail: string } }>('post', path); this.message.set(result.evidence.detail); } catch { this.message.set('Diagnostic could not be completed.'); } }
}
function lines(value: string): string[] { return value.split(/\r?\n/).map((item) => item.trim()).filter(Boolean); }
function isConflict(value: unknown): boolean { return typeof value === 'object' && value !== null && 'status' in value && (value as { status: number }).status === 409; }
