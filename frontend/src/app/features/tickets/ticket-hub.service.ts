import { Injectable, inject, NgZone, OnDestroy } from '@angular/core';
import { Subject, ReplaySubject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthStore } from '../../core/auth/auth.store';

export interface TicketEvent {
  ticketId: string;
  title?: string;
  field?: string;
  oldValue?: string | null;
  newValue?: string | null;
  taskId?: string;
  userId?: string;
  dueDate?: string;
  timestamp: string;
}

/**
 * SignalR connection to /hubs/tickets. Reconnects automatically with backoff,
 * re-emits events as observables so components subscribe with plain RxJS
 * operators (filter) without touching the connection itself.
 */
@Injectable({ providedIn: 'root' })
export class TicketHubService implements OnDestroy {
  private readonly authStore = inject(AuthStore);
  private readonly zone = inject(NgZone);

  private connection: HubConnection | null = null;
  private started = false;

  readonly ticketCreated = new Subject<TicketEvent>();
  readonly ticketUpdated = new Subject<TicketEvent>();
  readonly followupAdded = new Subject<TicketEvent>();
  readonly slaBreach = new Subject<TicketEvent>();
  readonly connectionState = new ReplaySubject<boolean>(1);

  async start(): Promise<void> {
    if (this.started) return;
    this.started = true;

    const url = `${environment.apiUrl.replace(/\/api$/, '')}/hubs/tickets`;

    this.connection = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.authStore.accessToken() ?? '',
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('TicketCreated', (e: TicketEvent) =>
      this.zone.run(() => this.ticketCreated.next(e))
    );
    this.connection.on('TicketUpdated', (e: TicketEvent) =>
      this.zone.run(() => this.ticketUpdated.next(e))
    );
    this.connection.on('FollowupAdded', (e: TicketEvent) =>
      this.zone.run(() => this.followupAdded.next(e))
    );
    this.connection.on('SlaBreach', (e: TicketEvent) =>
      this.zone.run(() => this.slaBreach.next(e))
    );

    this.connection.onreconnected(() => this.zone.run(() => this.connectionState.next(true)));
    this.connection.onclose(() => this.zone.run(() => this.connectionState.next(false)));

    try {
      await this.connection.start();
      this.connectionState.next(true);
    } catch {
      // Server unreachable — components still work over HTTP; allow retry later.
      this.connectionState.next(false);
      this.started = false;
    }
  }

  async subscribeToTicket(ticketId: string): Promise<void> {
    if (this.connection && this.connection.state === 'Connected') {
      await this.connection.invoke('SubscribeToTicket', ticketId);
    }
  }

  async unsubscribeFromTicket(ticketId: string): Promise<void> {
    if (this.connection && this.connection.state === 'Connected') {
      await this.connection.invoke('UnsubscribeFromTicket', ticketId);
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.started = false;
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }
}
