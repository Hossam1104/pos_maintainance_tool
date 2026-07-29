import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { AgentApi, Capability, Config } from '../core/agent-api.service';
import { SettingsPageComponent } from './settings-page.component';

const configuration: Config = {
  sqlInstance: 'LOCALHOST', sqlUser: 'operator', hasSqlPassword: true, branchCode: 'B001', posNumber: '03', release: '10.4', clientName: 'DBS', apiBaseUrl: 'https://main.example', databases: ['branch'], services: ['rms'],
  downloader: { apiUrl: 'https://download.example', rdbServerIp: '10.0.0.8', rdbUsername: 'rdb', hasRdbPassword: true, knownBranchCodes: ['B001'], pollIntervalSeconds: 5, timeoutSeconds: 1800 }, version: 4,
};
const capabilities: Capability = { agentVersion: '1.0', operatingSystem: 'Windows', browseRoots: [{ rootId: 'backups', displayName: 'Managed backups' }] };

describe('SettingsPageComponent', () => {
  it('submits both replacement secrets once and immediately clears the form fields', async () => {
    const calls: unknown[] = [];
    const api = {
      get: async <T>(path: string): Promise<T> => (path === '/configuration' ? configuration : capabilities) as T,
      mutate: async <T>(_method: string, _path: string, body?: unknown): Promise<T> => { calls.push(body); return { ...configuration, version: 5 } as T; },
    };
    const component = await create(api);
    component.form.patchValue({ sqlPassword: 'sentinel-sql-password', rdbPassword: 'sentinel-rdb-password' });

    await component.save();

    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ sqlPassword: 'sentinel-sql-password', downloader: { rdbPassword: 'sentinel-rdb-password' } });
    expect(component.form.controls.sqlPassword.value).toBe('');
    expect(component.form.controls.rdbPassword.value).toBe('');
  });

  it('preserves non-secret edits after a version conflict while discarding submitted secrets', async () => {
    const api = {
      get: async <T>(path: string): Promise<T> => (path === '/configuration' ? configuration : capabilities) as T,
      mutate: async <T>(): Promise<T> => Promise.reject({ status: 409 }),
    };
    const component = await create(api);
    component.form.patchValue({ clientName: 'Unsaved client', sqlPassword: 'sentinel-sql-password', rdbPassword: 'sentinel-rdb-password' });

    await component.save();

    expect(component.form.controls.clientName.value).toBe('Unsaved client');
    expect(component.form.controls.sqlPassword.value).toBe('');
    expect(component.form.controls.rdbPassword.value).toBe('');
    expect(component.message()).toContain('still in this form');
  });
});

async function create(api: Pick<AgentApi, 'get' | 'mutate'>): Promise<any> {
  await TestBed.configureTestingModule({ imports: [SettingsPageComponent], providers: [{ provide: AgentApi, useValue: api }] }).compileComponents();
  const fixture = TestBed.createComponent(SettingsPageComponent);
  fixture.detectChanges();
  await fixture.componentInstance.ngOnInit();
  fixture.detectChanges();
  return fixture.componentInstance;
}
