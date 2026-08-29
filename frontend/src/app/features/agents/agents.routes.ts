import { Routes } from '@angular/router';

export const AGENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./agent-list/agent-list.component').then(
        (m) => m.AgentListComponent
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./agent-detail/agent-detail.component').then(
        (m) => m.AgentDetailComponent
      ),
  },
];
