import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards/auth.guard';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login-page.component').then((m) => m.LoginPageComponent),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'visitors',
        loadComponent: () =>
          import('./features/visitors/visitor-list-page.component').then((m) => m.VisitorListPageComponent),
      },
      { path: '', pathMatch: 'full', redirectTo: 'visitors' },
    ],
  },
  { path: '**', redirectTo: '' },
];