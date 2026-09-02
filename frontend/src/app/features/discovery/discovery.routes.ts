import { Routes } from '@angular/router';

export const DISCOVERY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./discovery-range-list/discovery-range-list.component').then(
        (m) => m.DiscoveryRangeListComponent
      ),
  },
  {
    path: 'scans/:id',
    loadComponent: () =>
      import('./scan-results/scan-results.component').then(
        (m) => m.ScanResultsComponent
      ),
  },
  {
    path: 'devices/:id',
    loadComponent: () =>
      import('./device-detail/device-detail.component').then(
        (m) => m.DeviceDetailComponent
      ),
  },
  {
    path: 'network-map',
    loadComponent: () =>
      import('./network-map/network-map.component').then(
        (m) => m.NetworkMapComponent
      ),
  },
];
