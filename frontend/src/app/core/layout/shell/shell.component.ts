import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { AuthStore } from '../../auth/auth.store';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly authStore = inject(AuthStore);

  protected readonly navItems: NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Assets', icon: 'devices', route: '/assets' },
    { label: 'Agents', icon: 'computer', route: '/agents' },
    { label: 'Discovery', icon: 'radar', route: '/discovery' },
    { label: 'Tickets', icon: 'confirmation_number', route: '/tickets' },
    { label: 'Knowledge Base', icon: 'menu_book', route: '/knowledge-base' },
    { label: 'Reports', icon: 'bar_chart', route: '/reports' },
    { label: 'Administration', icon: 'admin_panel_settings', route: '/admin' },
  ];

  protected async onLogout(): Promise<void> {
    await this.authStore.logout();
  }
}