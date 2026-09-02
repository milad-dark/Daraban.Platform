import { Component, Input, OnInit, OnChanges, SimpleChanges, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DiscoveredDevice } from '../models/discovery.models';

interface NetworkNode {
  id: string;
  label: string;
  x: number;
  y: number;
  type: 'router' | 'switch' | 'server' | 'workstation' | 'printer' | 'unknown';
  status: 'online' | 'offline';
  device: DiscoveredDevice;
}

interface NetworkEdge {
  source: string;
  target: string;
}

@Component({
  selector: 'app-network-map',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="network-map">
      <div class="map-header">
        <h3>Network Topology</h3>
        <div class="legend">
          <span class="legend-item"><span class="dot router"></span> Router</span>
          <span class="legend-item"><span class="dot switch"></span> Switch</span>
          <span class="legend-item"><span class="dot server"></span> Server</span>
          <span class="legend-item"><span class="dot workstation"></span> Workstation</span>
          <span class="legend-item"><span class="dot printer"></span> Printer</span>
          <span class="legend-item"><span class="dot unknown"></span> Unknown</span>
        </div>
      </div>
      
      <div class="map-container" #mapContainer>
        <svg [attr.width]="width" [attr.height]="height">
          <!-- Edges -->
          @for (edge of edges; track edge.source + edge.target) {
            <line
              [attr.x1]="getNode(edge.source)?.x"
              [attr.y1]="getNode(edge.source)?.y"
              [attr.x2]="getNode(edge.target)?.x"
              [attr.y2]="getNode(edge.target)?.y"
              stroke="rgba(255, 255, 255, 0.1)"
              stroke-width="2"
            />
          }
          
          <!-- Nodes -->
          @for (node of nodes; track node.id) {
            <g [attr.transform]="'translate(' + node.x + ',' + node.y + ')'"
               (click)="selectNode(node)"
               class="node-group">
              <circle
                [attr.r]="getNodeRadius(node)"
                [attr.fill]="getNodeColor(node)"
                [attr.stroke]="node.status === 'online' ? '#10b981' : '#ef4444'"
                stroke-width="2"
                class="node-circle"
              />
              <text
                [attr.y]="getNodeRadius(node) + 16"
                text-anchor="middle"
                fill="#fff"
                font-size="12"
              >
                {{ node.label }}
              </text>
              <text
                [attr.y]="4"
                text-anchor="middle"
                fill="#fff"
                font-size="10"
              >
                {{ getNodeIcon(node) }}
              </text>
            </g>
          }
        </svg>
      </div>

      @if (selectedNode) {
        <div class="node-tooltip">
          <div class="tooltip-header">
            <strong>{{ selectedNode.label }}</strong>
            <button class="close-btn" (click)="selectedNode = null">×</button>
          </div>
          <div class="tooltip-content">
            <div><span class="label">IP:</span> {{ selectedNode.device.ipAddress }}</div>
            <div><span class="label">OS:</span> {{ selectedNode.device.osGuess || 'Unknown' }}</div>
            <div><span class="label">Status:</span> {{ selectedNode.status }}</div>
            @if (selectedNode.device.hostname) {
              <div><span class="label">Hostname:</span> {{ selectedNode.device.hostname }}</div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .network-map {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 20px;
    }

    .map-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }

    .map-header h3 {
      margin: 0;
      color: #fff;
    }

    .legend {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.75rem;
      color: #a0a0b0;
    }

    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }

    .dot.router { background: #8b5cf6; }
    .dot.switch { background: #06b6d4; }
    .dot.server { background: #3b82f6; }
    .dot.workstation { background: #10b981; }
    .dot.printer { background: #f59e0b; }
    .dot.unknown { background: #6b7280; }

    .map-container {
      background: rgba(0, 0, 0, 0.2);
      border-radius: 12px;
      overflow: hidden;
      min-height: 400px;
    }

    .node-group {
      cursor: pointer;
    }

    .node-circle {
      transition: all 0.3s ease;
    }

    .node-group:hover .node-circle {
      filter: brightness(1.2);
      transform: scale(1.1);
    }

    .node-tooltip {
      position: absolute;
      bottom: 20px;
      left: 20px;
      background: rgba(0, 0, 0, 0.9);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 12px;
      padding: 16px;
      min-width: 200px;
      z-index: 10;
    }

    .tooltip-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }

    .tooltip-header strong {
      color: #fff;
    }

    .close-btn {
      background: none;
      border: none;
      color: #a0a0b0;
      font-size: 1.2rem;
      cursor: pointer;
    }

    .close-btn:hover {
      color: #fff;
    }

    .tooltip-content {
      color: #a0a0b0;
      font-size: 0.85rem;
    }

    .tooltip-content .label {
      color: #6b7280;
    }

    .tooltip-content div {
      margin-bottom: 4px;
    }
  `]
})
export class NetworkMapComponent implements OnInit, OnChanges, AfterViewInit {
  @Input() devices: DiscoveredDevice[] = [];
  @ViewChild('mapContainer') mapContainer!: ElementRef;

  nodes: NetworkNode[] = [];
  edges: NetworkEdge[] = [];
  selectedNode: NetworkNode | null = null;
  width = 800;
  height = 400;

  ngOnInit(): void {
    this.buildGraph();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['devices']) {
      this.buildGraph();
    }
  }

  ngAfterViewInit(): void {
    this.updateDimensions();
  }

  buildGraph(): void {
    if (!this.devices.length) return;

    this.nodes = this.devices.map((device, index) => ({
      id: device.id.toString(),
      label: device.hostname || device.ipAddress,
      x: this.calculateX(index, this.devices.length),
      y: this.calculateY(index, this.devices.length),
      type: this.getNodeType(device),
      status: device.lastSeenAt ? 'online' : 'offline',
      device
    }));

    // Create edges based on IP subnet
    this.edges = [];
    for (let i = 0; i < this.nodes.length; i++) {
      for (let j = i + 1; j < this.nodes.length; j++) {
        if (this.areInSameSubnet(this.nodes[i].device.ipAddress, this.nodes[j].device.ipAddress)) {
          this.edges.push({
            source: this.nodes[i].id,
            target: this.nodes[j].id
          });
        }
      }
    }
  }

  calculateX(index: number, total: number): number {
    const padding = 80;
    const usableWidth = this.width - (padding * 2);
    return padding + (index % Math.ceil(Math.sqrt(total))) * (usableWidth / Math.ceil(Math.sqrt(total)));
  }

  calculateY(index: number, total: number): number {
    const padding = 50;
    const usableHeight = this.height - (padding * 2);
    return padding + Math.floor(index / Math.ceil(Math.sqrt(total))) * (usableHeight / Math.ceil(Math.sqrt(total)));
  }

  getNodeType(device: DiscoveredDevice): NetworkNode['type'] {
    const os = device.osGuess?.toLowerCase() || '';
    const ports = device.openPorts || '';
    
    if (ports.includes('161') || ports.includes('162')) return 'switch';
    if (os.includes('windows server') || os.includes('linux')) return 'server';
    if (os.includes('printer') || os.includes('hp') || ports.includes('9100')) return 'printer';
    if (os.includes('windows')) return 'workstation';
    if (os.includes('cisco') || os.includes('juniper')) return 'router';
    return 'unknown';
  }

  getNodeRadius(node: NetworkNode): number {
    return node.type === 'router' ? 25 : 
           node.type === 'switch' ? 22 : 
           node.type === 'server' ? 20 : 16;
  }

  getNodeColor(node: NetworkNode): string {
    const colors: Record<NetworkNode['type'], string> = {
      router: '#8b5cf6',
      switch: '#06b6d4',
      server: '#3b82f6',
      workstation: '#10b981',
      printer: '#f59e0b',
      unknown: '#6b7280'
    };
    return colors[node.type];
  }

  getNodeIcon(node: NetworkNode): string {
    const icons: Record<NetworkNode['type'], string> = {
      router: '🌐',
      switch: '🔀',
      server: '🖥️',
      workstation: '💻',
      printer: '🖨️',
      unknown: '❓'
    };
    return icons[node.type];
  }

  getNode(id: string): NetworkNode | undefined {
    return this.nodes.find(n => n.id === id);
  }

  selectNode(node: NetworkNode): void {
    this.selectedNode = this.selectedNode?.id === node.id ? null : node;
  }

  areInSameSubnet(ip1: string, ip2: string): boolean {
    // Simple /24 subnet check
    const parts1 = ip1.split('.');
    const parts2 = ip2.split('.');
    return parts1.slice(0, 3).join('.') === parts2.slice(0, 3).join('.');
  }

  private updateDimensions(): void {
    if (this.mapContainer?.nativeElement) {
      const rect = this.mapContainer.nativeElement.getBoundingClientRect();
      this.width = Math.max(rect.width, 600);
      this.height = Math.max(rect.height, 400);
    }
  }
}
