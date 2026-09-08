import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { KbStore } from '../../tickets/kb.store';

@Component({
  selector: 'app-kb-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressBarModule,
    MatButtonModule,
    MatCardModule,
  ],
  template: `
    <div class="kb-search-page">
      <div class="page-header">
        <h1>Knowledge Base</h1>
      </div>

      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Search articles</mat-label>
        <input matInput [formControl]="searchControl" placeholder="How do I reset my password?" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      @if (kb.isSearching()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (kb.searchQuery() && !kb.isSearching()) {
        <p class="result-count">{{ kb.searchResults().length }} results for "{{ kb.searchQuery() }}"</p>
        <div class="results">
          @for (hit of kb.searchResults(); track hit.article.id) {
            <mat-card class="result-card" (click)="open(hit.article.id)">
              <h2>{{ hit.article.title }}</h2>
              @if (hit.article.summary) {
                <p class="summary">{{ hit.article.summary }}</p>
              }
              <div class="meta">
                <span>{{ hit.article.helpfulCount }} found helpful</span>
                <span>{{ hit.article.viewCount }} views</span>
                <span>Relevance: {{ (hit.rank * 100).toFixed(0) }}%</span>
              </div>
            </mat-card>
          } @empty {
            <p class="empty">No articles match your search.</p>
          }
        </div>
      }

      @if (kb.error()) {
        <div class="error-banner">
          <mat-icon>error</mat-icon>
          {{ kb.error() }}
        </div>
      }
    </div>
  `,
  styles: [`
    .kb-search-page { padding: 24px; max-width: 800px; margin: 0 auto; }
    .page-header { margin-bottom: 16px; }
    h1 { margin: 0; font-size: 1.75rem; color: #fff; }
    .search-field { width: 100%; margin-bottom: 16px; }
    .result-count { color: #a0a0b0; font-size: 0.875rem; }
    .results { display: flex; flex-direction: column; gap: 12px; }
    .result-card { padding: 16px; cursor: pointer; transition: border-color 0.2s; }
    .result-card:hover { border-color: rgba(59, 130, 246, 0.5); }
    .result-card h2 { margin: 0 0 8px; font-size: 1rem; color: #3b82f6; }
    .summary { margin: 0 0 8px; color: #d1d5db; font-size: 0.875rem; }
    .meta { display: flex; gap: 16px; color: #6b7280; font-size: 0.75rem; }
    .empty { text-align: center; color: #6b7280; padding: 24px; }
    .error-banner {
      display: flex; align-items: center; gap: 12px;
      background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 8px; padding: 12px 16px; margin-top: 16px; color: #ef4444;
    }
  `],
})
export class KbSearchComponent implements OnInit, OnDestroy {
  protected readonly kb = inject(KbStore);
  private readonly router = inject(Router);

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    void this.kb.loadArticles();
    void this.kb.loadCategories();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), takeUntil(this.destroy$))
      .subscribe((q) => {
        if (q.trim()) {
          void this.kb.search(q);
        } else {
          this.kb.clearSearch();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  open(articleId: string): void {
    this.router.navigate(['/knowledge-base', articleId]);
  }
}
