import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Evidence { freshness: 'unknown' | 'fresh' | 'stale'; lastCheckedUtc: string | null; detail: string; }
export interface Config { sqlInstance: string; sqlUser: string; hasSqlPassword: boolean; branchCode: string; posNumber: string; release: string; clientName: string; apiBaseUrl: string; databases: string[]; services: string[]; downloader: { apiUrl: string; rdbServerIp: string; rdbUsername: string; hasRdbPassword: boolean; knownBranchCodes: string[]; pollIntervalSeconds: number; timeoutSeconds: number; }; version: number; }
export interface Identity { branchCode: string; posNumber: string; release: string; clientName: string; }
export interface Connectivity { localSql: Evidence; mainServer: Evidence; }
export interface Capability { agentVersion: string; operatingSystem: string; browseRoots: { rootId: string; displayName: string }[]; }
export interface Operation { operationId: string; operationType: string; state: string; progressPercent: number; currentStage: string; requestedAtUtc: string; }
export interface ActivityRecord { activityId: string; atUtc: string; category: string; summary: string; correlationId: string | null; isDestructive: boolean; }
export interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; }
export interface BrowseResult { rootId: string; relativeSubPath: string; entries: { name: string; isDirectory: boolean; relativeSubPath: string; sizeBytes: number | null; lastModifiedUtc: string | null; }[]; }

@Injectable({ providedIn: 'root' })
export class AgentApi {
  private readonly http = inject(HttpClient);
  get<T>(path: string): Promise<T> { return firstValueFrom(this.http.get<T>(`/api/v1${path}`)); }
  async mutate<T>(method: 'post' | 'put', path: string, body?: unknown): Promise<T> {
    const token = await firstValueFrom(this.http.get<{ token: string }>('/api/v1/antiforgery'));
    const options = { headers: new HttpHeaders({ 'X-CSRF-TOKEN': token.token }) };
    return firstValueFrom(method === 'post' ? this.http.post<T>(`/api/v1${path}`, body ?? {}, options) : this.http.put<T>(`/api/v1${path}`, body, options));
  }
}
