import { Component, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AgentApi, Config } from '../core/agent-api.service';

@Component({ standalone: true, imports: [ReactiveFormsModule], template: `
  <section class="route-heading"><span class="eyebrow">AGENT-OWNED CONFIGURATION</span><h1>Settings</h1><p>Passwords are write-only: blank keeps the retained secret; Clear removes it explicitly.</p></section>
  @if (message()) { <p class="error-state">{{ message() }}</p> }
  <form class="settings-form instrument-panel" [formGroup]="form" (ngSubmit)="save()">
    <label>Branch code <input formControlName="branchCode" maxlength="50" required></label><label>POS number <input formControlName="posNumber" maxlength="50"></label><label>Release <input formControlName="release"></label><label>Client name <input formControlName="clientName"></label>
    <label>SQL instance <input formControlName="sqlInstance"></label><label>SQL user <input formControlName="sqlUser"></label><label>SQL password ({{ config()?.hasSqlPassword ? 'stored' : 'not set' }}) <input type="password" formControlName="sqlPassword" autocomplete="new-password"></label>
    <label>Main-server URL <input formControlName="apiBaseUrl" placeholder="https://server"></label><label>Backup folder <input formControlName="backupFolder"></label>
    <div class="form-actions"><button class="primary-action" type="submit" [disabled]="form.invalid || saving()">Save configuration</button><button type="button" (click)="clear()" [disabled]="!config()?.hasSqlPassword">Clear SQL password</button><button type="button" (click)="importRms()">Import RMS+</button><button type="button" (click)="testDatabase()">Test DB</button><button type="button" (click)="verifyBranch()">Verify branch</button></div>
  </form>
`, })
export class SettingsPageComponent implements OnInit {
  private readonly api = inject(AgentApi); private readonly fb = inject(FormBuilder); protected readonly config = signal<Config | null>(null); protected readonly message = signal(''); protected readonly saving = signal(false);
  protected readonly form = this.fb.nonNullable.group({ branchCode: ['', [Validators.required, Validators.maxLength(50)]], posNumber: ['', Validators.maxLength(50)], release: [''], clientName: [''], sqlInstance: [''], sqlUser: [''], sqlPassword: [''], apiBaseUrl: ['', Validators.pattern(/^$|https?:\/\/.+/)], backupFolder: [''] });
  async ngOnInit(): Promise<void> { await this.load(); }
  async save(): Promise<void> { const current = this.config(); if (!current || this.form.invalid) return; this.saving.set(true); this.message.set(''); try { const value = this.form.getRawValue(); const updated = await this.api.mutate<Config>('put', '/configuration', { ...current, ...value, sqlPassword: value.sqlPassword || null, downloader: { ...current.downloader, rdbPassword: null }, expectedVersion: current.version }); this.config.set(updated); this.form.patchValue({ sqlPassword: '' }); this.form.markAsPristine(); this.message.set('Configuration saved.'); } catch (error: unknown) { this.message.set(isConflict(error) ? 'Configuration changed elsewhere. Your unsaved values are still in this form; reload only when ready.' : 'Configuration could not be saved.'); } finally { this.saving.set(false); } }
  async clear(): Promise<void> { const current = this.config(); if (!current) return; try { const updated = await this.api.mutate<Config>('post', '/configuration/secrets/clear', { secret: 'sqlPassword', expectedVersion: current.version }); this.config.set(updated); this.message.set('SQL password cleared.'); } catch { this.message.set('Secret could not be cleared.'); } }
  async importRms(): Promise<void> { if (this.form.dirty && !window.confirm('Import will replace the form with imported non-secret values. Continue?')) return; try { this.config.set(await this.api.mutate<Config>('post', '/configuration/import-rms')); this.patch(); this.message.set('RMS configuration imported.'); } catch { this.message.set('RMS import failed.'); } }
  async testDatabase(): Promise<void> { await this.run('/configuration/test-database'); } async verifyBranch(): Promise<void> { await this.run('/configuration/verify-branch'); }
  private async load(): Promise<void> { try { this.config.set(await this.api.get<Config>('/configuration')); this.patch(); } catch { this.message.set('Agent configuration is unavailable.'); } }
  private patch(): void { const c = this.config(); if (c) this.form.patchValue(c); this.form.markAsPristine(); }
  private async run(path: string): Promise<void> { try { const result = await this.api.mutate<{ evidence: { detail: string } }>('post', path); this.message.set(result.evidence.detail); } catch { this.message.set('Diagnostic could not be completed.'); } }
}
function isConflict(value: unknown): boolean { return typeof value === 'object' && value !== null && 'status' in value && (value as { status: number }).status === 409; }
