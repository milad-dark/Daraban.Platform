import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Plugin, PluginMenuItem } from './models/plugin.model';

/**
 * Access to the plugin registry API (Task 7.5). The backend enforces
 * `plugins.read` / `plugins.write`; a 403 surfaces through extractError in the
 * store, exactly like every other feature's error path.
 *
 * Install is a multipart upload of the .zip package; the server caps size
 * (20 MB), entry count, and uncompressed bytes before extracting anything.
 */
@Injectable({ providedIn: 'root' })
export class PluginsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/plugins`;

  /** All installed plugins (enabled and disabled; tombstones are omitted). */
  list(): Observable<Plugin[]> {
    return this.http.get<Plugin[]>(this.baseUrl);
  }

  /** Installs a plugin package (.zip). */
  install(packageFile: File): Observable<Plugin> {
    const formData = new FormData();
    formData.append('package', packageFile);
    return this.http.post<Plugin>(this.baseUrl, formData);
  }

  /** Loads and enables an installed plugin. */
  enable(pluginId: string): Observable<Plugin> {
    return this.http.post<Plugin>(
      `${this.baseUrl}/${encodeURIComponent(pluginId)}/enable`,
      {}
    );
  }

  /** Disables a plugin: unloads runtime state, keeps package + schema. */
  disable(pluginId: string): Observable<Plugin> {
    return this.http.post<Plugin>(
      `${this.baseUrl}/${encodeURIComponent(pluginId)}/disable`,
      {}
    );
  }

  /** Uninstalls: unloads, drops the isolated schema, deletes files, keeps a
   * registry tombstone for audit. Irreversible. */
  uninstall(pluginId: string): Observable<Plugin> {
    return this.http.delete<Plugin>(
      `${this.baseUrl}/${encodeURIComponent(pluginId)}`
    );
  }

  /** Menu items contributed by enabled plugins (permission-filtered). */
  menuItems(): Observable<PluginMenuItem[]> {
    return this.http.get<PluginMenuItem[]>(`${this.baseUrl}/menu-items`);
  }
}
