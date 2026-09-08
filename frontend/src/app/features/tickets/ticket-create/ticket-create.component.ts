import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TicketStore } from '../ticket.store';
import { KbStore } from '../kb.store';
import {
  CreateTicketRequest,
  TicketSource,
  TICKET_TYPE_OPTIONS,
  TICKET_PRIORITY_OPTIONS,
  TICKET_IMPACT_OPTIONS,
  TICKET_URGENCY_OPTIONS,
  TICKET_SOURCE_OPTIONS,
} from '../models/ticket.models';

@Component({
  selector: 'app-ticket-create',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="ticket-create-page">
      <div class="page-header">
        <h1>New Ticket</h1>
        <button mat-icon-button (click)="back()" matTooltip="Cancel">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Template picker -->
      <mat-card class="template-card">
        <h2>Start from a template</h2>
        <div class="template-row">
          @for (tpl of store.templates(); track tpl.id) {
            <button mat-stroked-button (click)="applyTemplate(tpl.id)">
              <mat-icon>description</mat-icon>
              {{ tpl.name }}
            </button>
          } @empty {
            <p class="no-templates">No templates available — fill the form manually.</p>
          }
        </div>
      </mat-card>

      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="create-form">
        <mat-card class="form-card">
          <!-- Title -->
          <mat-form-field appearance="outline" class="full">
            <mat-label>Title</mat-label>
            <input matInput formControlName="title" placeholder="Brief summary of the issue" />
            @if (form.controls['title'].hasError('required')) {
              <mat-error>Title is required</mat-error>
            }
          </mat-form-field>

          <!-- Inline KB search while typing the title -->
          @if (kb.searchResults().length > 0) {
            <div class="kb-suggestions">
              <h3>
                <mat-icon>lightbulb</mat-icon>
                Suggested articles — maybe this solves it already?
              </h3>
              @for (hit of kb.searchResults(); track hit.article.id) {
                <button type="button" mat-stroked-button (click)="openArticle(hit.article.id)">
                  {{ hit.article.title }}
                  <span class="kb-meta">{{ hit.article.helpfulCount }} found helpful</span>
                </button>
              }
            </div>
          }

          <!-- Description -->
          <mat-form-field appearance="outline" class="full">
            <mat-label>Description</mat-label>
            <textarea matInput formControlName="description" rows="6"
              placeholder="What happened? Steps to reproduce, expected vs actual behavior..."></textarea>
          </mat-form-field>

          <!-- ITIL classification -->
          <div class="field-row">
            <mat-form-field appearance="outline">
              <mat-label>Type</mat-label>
              <mat-select formControlName="type">
                @for (option of typeOptions; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Priority</mat-label>
              <mat-select formControlName="priority">
                @for (option of priorityOptions; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Impact</mat-label>
              <mat-select formControlName="impact">
                @for (option of impactOptions; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Urgency</mat-label>
              <mat-select formControlName="urgency">
                @for (option of urgencyOptions; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>

          <!-- Linking -->
          <div class="field-row">
            <mat-form-field appearance="outline">
              <mat-label>Asset (optional)</mat-label>
              <input matInput formControlName="assetId" placeholder="Asset GUID" />
              <mat-hint>Link the affected asset</mat-hint>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Source</mat-label>
              <mat-select formControlName="source">
                @for (option of sourceOptions; track option.value) {
                  <mat-option [value]="option.value">{{ option.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        </mat-card>

        <div class="form-actions">
          <button mat-stroked-button type="button" (click)="back()">Cancel</button>
          <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || store.isSaving()">
            @if (store.isSaving()) {
              Creating...
            } @else {
              <ng-container>
                <mat-icon>check</mat-icon>
                Create Ticket
              </ng-container>
            }
          </button>
        </div>
      </form>

      @if (store.error()) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ store.error() }}
        </div>
      }
    </div>
  `,
  styles: [`
    .ticket-create-page { padding: 24px; max-width: 900px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h1 { margin: 0; font-size: 1.5rem; color: #fff; }
    .template-card { padding: 16px; margin-bottom: 16px; }
    .template-card h2 { margin: 0 0 12px; font-size: 0.9375rem; color: #fff; }
    .template-row { display: flex; gap: 8px; flex-wrap: wrap; }
    .no-templates { color: #6b7280; font-size: 0.8125rem; margin: 0; }
    .form-card { padding: 24px; margin-bottom: 16px; }
    .full { width: 100%; }
    .field-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
    .kb-suggestions {
      margin: 4px 0 16px; padding: 12px;
      background: rgba(245, 158, 11, 0.06); border: 1px solid rgba(245, 158, 11, 0.25);
      border-radius: 10px;
    }
    .kb-suggestions h3 {
      display: flex; align-items: center; gap: 6px; margin: 0 0 8px;
      font-size: 0.8125rem; color: #f59e0b;
    }
    .kb-suggestions button { display: block; width: 100%; text-align: left; margin-bottom: 6px; justify-content: space-between; }
    .kb-meta { color: #6b7280; font-size: 0.75rem; margin-left: auto; }
    .form-actions { display: flex; justify-content: flex-end; gap: 12px; }
    .error-banner {
      display: flex; align-items: center; gap: 12px;
      background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 8px; padding: 12px 16px; margin-top: 16px; color: #ef4444;
    }
  `],
})
export class TicketCreateComponent implements OnInit, OnDestroy {
  protected readonly store = inject(TicketStore);
  protected readonly kb = inject(KbStore);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly typeOptions = TICKET_TYPE_OPTIONS;
  protected readonly priorityOptions = TICKET_PRIORITY_OPTIONS;
  protected readonly impactOptions = TICKET_IMPACT_OPTIONS;
  protected readonly urgencyOptions = TICKET_URGENCY_OPTIONS;
  protected readonly sourceOptions = TICKET_SOURCE_OPTIONS;

  protected readonly form: FormGroup = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    type: [1],
    priority: [2],
    impact: [2],
    urgency: [2],
    assetId: [''],
    source: [TicketSource.Helpdesk],
  });

  private appliedTemplateId: string | null = null;
  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.store.loadTemplates();

    // Live KB search while typing the title — surfaces articles that may
    // already solve the issue before the ticket is filed.
    this.form.controls['title'].valueChanges
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        switchMap((title) => {
          const q = (title ?? '').trim();
          return q.length >= 3 ? this.safeSearch(q) : [];
        }),
        takeUntil(this.destroy$)
      )
      .subscribe(() => undefined);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.kb.clearSearch();
  }

  private async safeSearch(query: string): Promise<unknown[]> {
    await this.kb.search(query);
    return this.kb.searchResults();
  }

  applyTemplate(templateId: string): void {
    const tpl = this.store.templates().find((t) => t.id === templateId);
    if (!tpl) return;
    this.appliedTemplateId = templateId;
    this.form.patchValue({
      type: tpl.defaultType,
      priority: tpl.defaultPriority,
      impact: tpl.defaultImpact,
      urgency: tpl.defaultUrgency,
      title: this.interpolate(tpl.titleTemplate) || this.form.controls['title'].value,
      description: this.interpolate(tpl.descriptionTemplate) || this.form.controls['description'].value,
    });
  }

  /** Substitutes {title}/{description} tokens left from a previous template pass. */
  private interpolate(text: string | null | undefined): string {
    if (!text) return '';
    return text;
  }

  openArticle(articleId: string): void {
    this.router.navigate(['/knowledge-base', articleId], {
      queryParams: { returnTo: 'tickets/new' },
    });
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) return;

    const v = this.form.getRawValue();
    const request: CreateTicketRequest = {
      type: v.type,
      priority: v.priority,
      impact: v.impact,
      urgency: v.urgency,
      title: v.title.trim(),
      description: v.description || null,
      requesterUserId: '', // resolved server-side from the JWT; empty guid is ignored
      assignedUserId: null,
      assignedGroupId: null,
      itilCategoryId: null,
      slaLevelId: null,
      assetId: v.assetId || null,
      locationId: null,
      source: v.source,
    };

    // Note: the backend overwrites requesterUserId from the JWT — the field in
    // the DTO is legacy; we send a valid Guid.empty placeholder.
    request.requesterUserId = '00000000-0000-0000-0000-000000000000';

    const id = await this.store.createTicket(request);
    if (id) {
      this.kb.clearSearch();
      this.router.navigate(['/tickets', id]);
    }
  }

  back(): void {
    this.router.navigate(['/tickets']);
  }
}
