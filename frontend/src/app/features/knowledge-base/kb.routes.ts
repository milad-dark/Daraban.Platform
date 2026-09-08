import { Routes } from '@angular/router';

export const KB_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./kb-search/kb-search.component').then((m) => m.KbSearchComponent),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./kb-article/kb-article.component').then((m) => m.KbArticleComponent),
  },
];
