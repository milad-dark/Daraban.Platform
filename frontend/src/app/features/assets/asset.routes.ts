import { Routes } from '@angular/router';

export const ASSET_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./asset-list/asset-list.component').then(
        (m) => m.AssetListComponent
      ),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./asset-form/asset-form.component').then(
        (m) => m.AssetFormComponent
      ),
  },
  {
    path: 'import',
    loadComponent: () =>
      import('./asset-import/asset-import.component').then(
        (m) => m.AssetImportComponent
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./asset-detail/asset-detail.component').then(
        (m) => m.AssetDetailComponent
      ),
  },
  {
    path: ':id/edit',
    loadComponent: () =>
      import('./asset-form/asset-form.component').then(
        (m) => m.AssetFormComponent
      ),
  },
];
