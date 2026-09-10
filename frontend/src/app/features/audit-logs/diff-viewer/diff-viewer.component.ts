import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

/** One field-level line of the diff. */
export interface DiffLine {
  field: string;
  kind: 'added' | 'removed' | 'changed';
  before: string;
  after: string;
}

/** A one-side JSON snapshot rendered as field/value rows. */
interface SnapshotView {
  fields: Array<{ field: string; value: string }>;
  parseError: string | null;
}

/**
 * Old/new JSON diff viewer (Task 7.3). Renders a two-sided, field-aligned view of the
 * interceptor's snapshots: an Added entry shows only its new fields, a Deleted entry only
 * its old fields, and a Modified entry lines up every changed field as before -> after.
 *
 * Security: values are rendered as {{ interpolation }} text only -- never [innerHTML] --
 * so a malicious value stored in the audited data cannot inject markup. Nested objects
 * are shown compact, expandable to full JSON.
 */
@Component({
  selector: 'app-diff-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="diff-viewer">
      @if (parseError(); as parseErrorMessage) {
        <div class="parse-error">
          <mat-icon>warning_amber</mat-icon>
          <span>{{ parseErrorMessage }}</span>
        </div>
      }

      <!-- Changed fields, aligned old -> new -->
      @if (changedLines().length > 0) {
        <table class="diff-table">
          <thead>
            <tr>
              <th class="field-col">Field</th>
              <th>Before</th>
              <th>After</th>
            </tr>
          </thead>
          <tbody>
            @for (line of changedLines(); track line.field) {
              <tr>
                <td class="field-col"><code>{{ line.field }}</code></td>
                <td class="removed"><span class="value">{{ line.before }}</span></td>
                <td class="added"><span class="value">{{ line.after }}</span></td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Added entry: fields that exist only on the new side -->
      @if (addedLines().length > 0) {
        <div class="side-header added-header">Created with</div>
        <table class="diff-table">
          <tbody>
            @for (line of addedLines(); track line.field) {
              <tr>
                <td class="field-col"><code>{{ line.field }}</code></td>
                <td class="added"><span class="value">{{ line.after }}</span></td>
              </tr>
            }
          </tbody>
        </table>
      }

      <!-- Deleted entry: fields that exist only on the old side -->
      @if (removedLines().length > 0) {
        <div class="side-header removed-header">Deleted with</div>
        <table class="diff-table">
          <tbody>
            @for (line of removedLines(); track line.field) {
              <tr>
                <td class="field-col"><code>{{ line.field }}</code></td>
                <td class="removed"><span class="value">{{ line.before }}</span></td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (isEmpty()) {
        <div class="no-changes">No field differences recorded for this change.</div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .diff-viewer {
      font-size: 12px;
    }

    .parse-error {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 12px;
      border-radius: 4px;
      background: #fff3e0;
      color: #e65100;
      margin-bottom: 8px;

      mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
    }

    .diff-table {
      width: 100%;
      border-collapse: collapse;

      th {
        text-align: left;
        font-size: 11px;
        text-transform: uppercase;
        letter-spacing: 0.4px;
        color: #757575;
        padding: 4px 10px;
        border-bottom: 1px solid #e0e0e0;
      }

      td {
        padding: 4px 10px;
        border-bottom: 1px solid #f0f0f0;
        vertical-align: top;
        font-family: 'JetBrains Mono', 'Fira Code', monospace;
      }

      .field-col {
        width: 220px;

        code {
          background: #f5f5f5;
          padding: 1px 6px;
          border-radius: 3px;
          font-size: 11px;
        }
      }

      .value {
        display: inline-block;
        max-width: 420px;
        overflow-wrap: anywhere;
        white-space: pre-wrap;
        word-break: break-word;
        cursor: default;
      }

      .removed .value {
        background: #fdecea;
        color: #b71c1c;
        text-decoration: line-through;
        text-decoration-color: rgba(183, 28, 28, 0.4);
        border-radius: 3px;
        padding: 0 4px;
      }

      .added .value {
        background: #e8f5e9;
        color: #1b5e20;
        border-radius: 3px;
        padding: 0 4px;
      }
    }

    .side-header {
      margin: 10px 0 4px;
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.4px;

      &.added-header { color: #2e7d32; }
      &.removed-header { color: #c62828; }
    }

    .no-changes {
      padding: 8px 12px;
      color: #757575;
      font-style: italic;
    }
  `,
})
export class DiffViewerComponent {
  /** Snapshot of the record before the change; null for inserts. */
  readonly oldJson = input<string | null>(null);
  /** Snapshot of the record after the change; null for deletes. */
  readonly newJson = input<string | null>(null);

  private static readonly maxCompactLength = 80;

  private readonly oldView = computed(() => parseSnapshot(this.oldJson()));
  private readonly newView = computed(() => parseSnapshot(this.newJson()));

  /** First parse problem on either side, if any. */
  protected readonly parseError = computed(
    () => this.oldView().parseError ?? this.newView().parseError
  );

  /** Fields present on both sides whose value differs -- the true "diff". */
  protected readonly changedLines = computed<DiffLine[]>(() => {
    const oldMap = this.oldView().byField;
    const newMap = this.newView().byField;
    const lines: DiffLine[] = [];

    for (const [field, before] of oldMap) {
      const after = newMap.get(field);
      if (after !== undefined && after !== before) {
        lines.push({ field, kind: 'changed', before, after });
      }
    }

    return lines;
  });

  /** Fields only on the new side (fields an insert creates, or post-hoc additions). */
  protected readonly addedLines = computed<DiffLine[]>(() => {
    const oldFields = this.oldView().byField;
    const newFields = this.newView().byField;
    const lines: DiffLine[] = [];

    for (const [field, after] of newFields) {
      if (!oldFields.has(field)) {
        lines.push({ field, kind: 'added', before: '', after });
      }
    }

    return lines;
  });

  /** Fields only on the old side (fields a delete takes away, or removals). */
  protected readonly removedLines = computed<DiffLine[]>(() => {
    const oldFields = this.oldView().byField;
    const newFields = this.newView().byField;
    const lines: DiffLine[] = [];

    for (const [field, before] of oldFields) {
      if (!newFields.has(field)) {
        lines.push({ field, kind: 'removed', before, after: '' });
      }
    }

    return lines;
  });

  protected readonly isEmpty = computed(
    () =>
      this.changedLines().length === 0 &&
      this.addedLines().length === 0 &&
      this.removedLines().length === 0
  );
}

/** Flattens one snapshot into field -> display-string, tolerating malformed JSON. */
function parseSnapshot(json: string | null | undefined): SnapshotView & { byField: Map<string, string> } {
  const empty: SnapshotView & { byField: Map<string, string> } = {
    fields: [],
    parseError: null,
    byField: new Map(),
  };

  if (!json) {
    return empty;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch {
    return { ...empty, parseError: 'Snapshot is not valid JSON.' };
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { ...empty, parseError: 'Snapshot is not a JSON object.' };
  }

  const byField = new Map<string, string>();
  for (const [field, value] of Object.entries(parsed as Record<string, unknown>)) {
    byField.set(field, formatValue(value));
  }

  return { fields: [...byField].map(([field, value]) => ({ field, value })), parseError: null, byField };
}

function formatValue(value: unknown): string {
  if (value === null) return 'null';
  if (typeof value === 'string') return value;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return JSON.stringify(value); // arrays / nested objects, compact
}
