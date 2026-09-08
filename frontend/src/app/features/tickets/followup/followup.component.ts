import {
  Component,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TicketTask, TicketTaskType } from '../models/ticket.models';

/**
 * Followup thread entry: rich-text-capable comment editor (contenteditable for
 * bold/italic/lists) posting a TicketTask. Private notes are flagged so the
 * backend keeps them agent-only.
 */
@Component({
  selector: 'app-followup',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatSelectModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="followup-editor">
      <!-- Formatting toolbar -->
      <div class="toolbar">
        <button type="button" mat-icon-button (click)="exec('bold')" matTooltip="Bold">
          <mat-icon>format_bold</mat-icon>
        </button>
        <button type="button" mat-icon-button (click)="exec('italic')" matTooltip="Italic">
          <mat-icon>format_italic</mat-icon>
        </button>
        <button type="button" mat-icon-button (click)="exec('insertUnorderedList')" matTooltip="Bullet list">
          <mat-icon>format_list_bulleted</mat-icon>
        </button>
        <button type="button" mat-icon-button (click)="exec('insertOrderedList')" matTooltip="Numbered list">
          <mat-icon>format_list_numbered</mat-icon>
        </button>
      </div>

      <!-- Editor -->
      <div
        class="editor"
        contenteditable="true"
        role="textbox"
        aria-multiline="true"
        [attr.aria-label]="'Followup content'"
        [innerHTML]="initialContent"
        (input)="onInput($event)"
        data-placeholder="Write a followup...">
      </div>

      <!-- Options row -->
      <div class="options-row">
        <mat-form-field appearance="outline" class="type-field">
          <mat-label>Type</mat-label>
          <mat-select [(ngModel)]="taskType">
            <mat-option [value]="taskTypes.Comment">Comment</mat-option>
            <mat-option [value]="taskTypes.Action">Action</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="time-field">
          <mat-label>Time spent (min)</mat-label>
          <input matInput type="number" min="0" [(ngModel)]="timeSpent" />
        </mat-form-field>

        <mat-checkbox [(ngModel)]="isPrivate" class="private-toggle">Private note</mat-checkbox>

        <span class="spacer"></span>

        <button
          mat-flat-button
          color="primary"
          [disabled]="!hasContent() || submitting"
          (click)="submit()">
          @if (submitting) {
            <mat-progress-spinner diameter="18" mode="indeterminate"></mat-progress-spinner>
          } @else {
            <mat-icon>send</mat-icon>
          }
          Post Followup
        </button>
      </div>
    </div>

    <!-- Thread -->
    <div class="thread">
      @for (task of tasks; track task.id) {
        <div class="followup-item" [class.private]="task.isPrivate">
          <div class="followup-header">
            <span class="author">User {{ task.userId.slice(0, 8) }}</span>
            <span class="task-type" [class.private-tag]="task.isPrivate">
              {{ task.isPrivate ? 'Private' : taskTypeLabel(task.type) }}
            </span>
            <span class="timestamp">{{ task.createdAt | date: 'short' }}</span>
            @if (task.timeSpentMinutes) {
              <span class="time-spent">{{ task.timeSpentMinutes }} min</span>
            }
          </div>
          <div class="followup-content" [innerHTML]="task.content"></div>
        </div>
      } @empty {
        <p class="empty-thread">No followups yet. Be the first to respond.</p>
      }
    </div>
  `,
  styles: [`
    .followup-editor {
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 12px;
      background: rgba(255, 255, 255, 0.03);
      overflow: hidden;
      margin-bottom: 24px;
    }
    .toolbar {
      display: flex; gap: 4px; padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .editor {
      min-height: 100px; padding: 14px; outline: none;
      font-size: 0.9375rem; line-height: 1.6; color: #e5e7eb;
    }
    .editor:empty::before {
      content: attr(data-placeholder); color: #6b7280;
    }
    .options-row {
      display: flex; gap: 12px; align-items: center; flex-wrap: wrap;
      padding: 12px; border-top: 1px solid rgba(255, 255, 255, 0.08);
    }
    .type-field { width: 140px; }
    .time-field { width: 150px; }
    .private-toggle { margin-left: 4px; }
    .spacer { flex: 1; }
    .thread { display: flex; flex-direction: column; gap: 12px; }
    .followup-item {
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 10px; padding: 14px;
      background: rgba(255, 255, 255, 0.02);
    }
    .followup-item.private {
      border-color: rgba(245, 158, 11, 0.3);
      background: rgba(245, 158, 11, 0.05);
    }
    .followup-header {
      display: flex; gap: 12px; align-items: center; margin-bottom: 8px; flex-wrap: wrap;
    }
    .author { font-weight: 600; color: #fff; font-size: 0.875rem; }
    .task-type {
      font-size: 0.6875rem; padding: 2px 8px; border-radius: 10px;
      background: rgba(59, 130, 246, 0.15); color: #3b82f6;
    }
    .private-tag { background: rgba(245, 158, 11, 0.15); color: #f59e0b; }
    .timestamp { color: #6b7280; font-size: 0.75rem; }
    .time-spent { color: #a0a0b0; font-size: 0.75rem; }
    .followup-content { color: #d1d5db; font-size: 0.9375rem; line-height: 1.6; }
    .empty-thread { text-align: center; color: #6b7280; padding: 24px; }
  `],
})
export class FollowupComponent {
  @Input() tasks: TicketTask[] = [];
  @Input() submitting = false;
  @Output() postFollowup = new EventEmitter<{
    content: string;
    type: TicketTaskType;
    timeSpentMinutes: number | null;
    isPrivate: boolean;
  }>();

  readonly taskTypes = TicketTaskType;
  taskType: TicketTaskType = TicketTaskType.Comment;
  timeSpent: number | null = null;
  isPrivate = false;
  initialContent = '';

  private editorContent = '';

  onInput(event: Event): void {
    this.editorContent = (event.target as HTMLElement).innerHTML;
  }

  hasContent(): boolean {
    const text = this.editorContent.replace(/<[^>]*>/g, '').trim();
    return text.length > 0;
  }

  exec(command: string): void {
    document.execCommand(command);
  }

  taskTypeLabel(type: number): string {
    return TicketTaskType[type] ?? 'Comment';
  }

  submit(): void {
    if (!this.hasContent()) return;
    this.postFollowup.emit({
      content: this.editorContent,
      type: this.taskType,
      timeSpentMinutes: this.timeSpent,
      isPrivate: this.isPrivate,
    });
    // Reset editor after posting.
    this.editorContent = '';
    this.initialContent = '';
    this.timeSpent = null;
    this.isPrivate = false;
    const editor = document.querySelector('.editor');
    if (editor) (editor as HTMLElement).innerHTML = '';
  }
}
