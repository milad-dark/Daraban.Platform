import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { KbStore } from '../../tickets/kb.store';

@Component({
  selector: 'app-kb-article',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="kb-article-page">
      @if (kb.isLoadingDetail()) {
        <mat-progress-bar mode="indeterminate"></mat-progress-bar>
      }

      @if (article(); as a) {
        <div class="article-header">
          <button mat-icon-button (click)="back()" matTooltip="Back">
            <mat-icon>arrow_back</mat-icon>
          </button>
          <div>
            @if (a.categoryName) {
              <span class="category">{{ a.categoryName }}</span>
            }
            @if (a.isFaq) {
              <span class="faq-badge">FAQ</span>
            }
            <h1>{{ a.title }}</h1>
            <p class="meta">
              By {{ a.authorUserId.slice(0, 8) }}
              @if (a.publishedAt) { · Published {{ a.publishedAt | date: 'mediumDate' }} }
              · {{ a.viewCount }} views · Updated {{ a.updatedAt | date: 'mediumDate' }}
            </p>
          </div>
        </div>

        <mat-card class="article-body">
          @if (a.summary) {
            <div class="summary">{{ a.summary }}</div>
          }
          <div class="content" [innerHTML]="a.content"></div>
          @if (a.tags) {
            <div class="tags">
              @for (tag of a.tags.split(','); track tag) {
                <span class="tag">{{ tag.trim() }}</span>
              }
            </div>
          }
        </mat-card>

        <!-- Feedback -->
        <div class="feedback">
          <p>Was this article helpful?</p>
          <div class="feedback-buttons">
            <button mat-stroked-button (click)="onFeedback(true)" matTooltip="Yes, it helped">
              <mat-icon>thumb_up</mat-icon>
              {{ a.helpfulCount }}
            </button>
            <button mat-stroked-button (click)="onFeedback(false)" matTooltip="No, it didn't help">
              <mat-icon>thumb_down</mat-icon>
              {{ a.notHelpfulCount }}
            </button>
          </div>
          @if (feedbackSubmitted) {
            <p class="thanks">Thanks for your feedback!</p>
          }
        </div>
      } @else if (!kb.isLoadingDetail()) {
        <div class="empty-state">
          <mat-icon>article</mat-icon>
          <p>Article not found.</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .kb-article-page { padding: 24px; max-width: 800px; margin: 0 auto; }
    .article-header { display: flex; gap: 12px; align-items: flex-start; margin-bottom: 16px; }
    .category {
      display: inline-block; padding: 2px 10px; border-radius: 10px; font-size: 0.75rem;
      background: rgba(59, 130, 246, 0.15); color: #3b82f6; margin-right: 6px;
    }
    .faq-badge {
      display: inline-block; padding: 2px 10px; border-radius: 10px; font-size: 0.75rem;
      background: rgba(245, 158, 11, 0.15); color: #f59e0b;
    }
    h1 { margin: 8px 0; font-size: 1.5rem; color: #fff; }
    .meta { margin: 0; color: #6b7280; font-size: 0.8125rem; }
    .article-body { padding: 24px; }
    .summary {
      padding: 12px 16px; border-left: 3px solid #3b82f6;
      background: rgba(59, 130, 246, 0.05); border-radius: 0 8px 8px 0;
      color: #d1d5db; margin-bottom: 16px; font-style: italic;
    }
    .content { color: #e5e7eb; line-height: 1.7; }
    .tags { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 16px; }
    .tag {
      padding: 2px 10px; border-radius: 10px; font-size: 0.75rem;
      background: rgba(255, 255, 255, 0.06); color: #a0a0b0;
    }
    .feedback { text-align: center; margin-top: 24px; }
    .feedback p { color: #a0a0b0; }
    .feedback-buttons { display: flex; gap: 12px; justify-content: center; }
    .thanks { color: #10b981; font-size: 0.875rem; }
    .empty-state { text-align: center; padding: 64px 24px; color: #a0a0b0; }
  `],
})
export class KbArticleComponent implements OnInit {
  protected readonly kb = inject(KbStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected feedbackSubmitted = false;

  article() {
    return this.kb.selectedArticle();
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/knowledge-base']);
      return;
    }
    // countView=true from the reader view — increments the view counter.
    void this.kb.loadArticle(id, true);
  }

  async onFeedback(isHelpful: boolean): Promise<void> {
    const article = this.article();
    if (!article || this.feedbackSubmitted) return;
    const ok = await this.kb.submitFeedback(article.id, isHelpful);
    if (ok) this.feedbackSubmitted = true;
  }

  back(): void {
    this.router.navigate(['/knowledge-base']);
  }
}
