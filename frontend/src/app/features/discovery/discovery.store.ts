import { Injectable, signal, computed } from '@angular/core';
import {
  DiscoveryRange,
  DiscoveryScan,
  DiscoveredDevice,
  DiscoveryDashboard,
  ScanProgress
} from './models/discovery.models';

@Injectable({
  providedIn: 'root'
})
export class DiscoveryStore {
  // State
  readonly ranges = signal<DiscoveryRange[]>([]);
  readonly scans = signal<DiscoveryScan[]>([]);
  readonly devices = signal<DiscoveredDevice[]>([]);
  readonly dashboard = signal<DiscoveryDashboard | null>(null);
  readonly selectedRange = signal<DiscoveryRange | null>(null);
  readonly selectedScan = signal<DiscoveryScan | null>(null);
  readonly selectedDevice = signal<DiscoveredDevice | null>(null);
  readonly scanProgress = signal<ScanProgress | null>(null);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);

  // Computed
  readonly activeRanges = computed(() =>
    this.ranges().filter(r => r.isActive)
  );

  readonly recentScans = computed(() =>
    this.scans().slice(0, 10)
  );

  readonly onlineDevices = computed(() =>
    this.devices().filter(d => d.lastSeenAt)
  );

  readonly offlineDevices = computed(() =>
    this.devices().filter(d => !d.lastSeenAt)
  );

  // Actions
  setRanges(ranges: DiscoveryRange[]): void {
    this.ranges.set(ranges);
  }

  setScans(scans: DiscoveryScan[]): void {
    this.scans.set(scans);
  }

  setDevices(devices: DiscoveredDevice[]): void {
    this.devices.set(devices);
  }

  setDashboard(dashboard: DiscoveryDashboard): void {
    this.dashboard.set(dashboard);
  }

  setSelectedRange(range: DiscoveryRange | null): void {
    this.selectedRange.set(range);
  }

  setSelectedScan(scan: DiscoveryScan | null): void {
    this.selectedScan.set(scan);
  }

  setSelectedDevice(device: DiscoveredDevice | null): void {
    this.selectedDevice.set(device);
  }

  updateScanProgress(progress: ScanProgress): void {
    this.scanProgress.set(progress);
  }

  setLoading(loading: boolean): void {
    this.loading.set(loading);
  }

  setError(error: string | null): void {
    this.error.set(error);
  }

  addRange(range: DiscoveryRange): void {
    this.ranges.update(ranges => [...ranges, range]);
  }

  updateRange(updatedRange: DiscoveryRange): void {
    this.ranges.update(ranges =>
      ranges.map(r => r.id === updatedRange.id ? updatedRange : r)
    );
  }

  removeRange(id: string): void {
    this.ranges.update(ranges => ranges.filter(r => r.id !== id));
  }

  addDevice(device: DiscoveredDevice): void {
    this.devices.update(devices => [...devices, device]);
  }

  updateDevice(updatedDevice: DiscoveredDevice): void {
    this.devices.update(devices =>
      devices.map(d => d.id === updatedDevice.id ? updatedDevice : d)
    );
  }
}
