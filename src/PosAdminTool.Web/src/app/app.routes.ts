import { isDevMode } from '@angular/core';
import { Routes } from '@angular/router';

const page = () => import('./features/placeholder-page.component').then((m) => m.PlaceholderPageComponent);
export const routes: Routes = [
  { path: '', title: 'Overview - DBS POS Admin', loadComponent: () => import('./features/overview-page.component').then((m) => m.OverviewPageComponent) },
  { path: 'device', title: 'Device - DBS POS Admin', loadComponent: () => import('./features/device-page.component').then((m) => m.DevicePageComponent) },
  { path: 'settings', title: 'Settings - DBS POS Admin', loadComponent: () => import('./features/settings-page.component').then((m) => m.SettingsPageComponent) },
  { path: 'services', title: 'Services - DBS POS Admin', loadComponent: () => import('./features/services-page.component').then((m) => m.ServicesPageComponent) },
  ...['backups','restore','maintenance','downloads','activity'].map((path) => ({ path, title: `${path[0].toUpperCase()}${path.slice(1)} - DBS POS Admin`, data: { title: `${path[0].toUpperCase()}${path.slice(1)}`, detail: 'This workspace is prepared for its Agent-backed workflow in the next migration session.' }, loadComponent: page })),
  ...(isDevMode() ? [{ path: 'gallery', title: 'Component gallery - DBS POS Admin', loadComponent: () => import('./features/component-gallery.component').then((m) => m.ComponentGalleryComponent) }] : []),
  { path: '**', redirectTo: '' },
];
