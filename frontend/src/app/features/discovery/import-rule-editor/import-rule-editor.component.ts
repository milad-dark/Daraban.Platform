import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiscoveryService } from '../discovery.service';
import { DiscoveryStore } from '../discovery.store';

interface ImportRule {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
  priority: number;
  criteria: ImportRuleCriteria[];
  actions: ImportRuleAction[];
  createdAt: string;
  modifiedAt?: string;
}

interface ImportRuleCriteria {
  id?: string;
  field: string;
  operator: string;
  value: string;
}

interface ImportRuleAction {
  id?: string;
  actionType: string;
  value?: string;
}

@Component({
  selector: 'app-import-rule-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="import-rule-editor">
      <div class="header">
        <h1>Import Rules</h1>
        <button class="btn-primary" (click)="createNewRule()">
          + New Rule
        </button>
      </div>

      @if (editingRule) {
        <div class="editor-panel">
          <div class="editor-header">
            <h2>{{ editingRule.id ? 'Edit Rule' : 'New Rule' }}</h2>
            <button class="btn-close" (click)="cancelEdit()">×</button>
          </div>

          <div class="editor-body">
            <!-- Basic Info -->
            <div class="form-group">
              <label>Name</label>
              <input type="text" [(ngModel)]="editingRule.name" placeholder="Rule name">
            </div>

            <div class="form-group">
              <label>Description</label>
              <textarea [(ngModel)]="editingRule.description" placeholder="Optional description"></textarea>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Priority</label>
                <input type="number" [(ngModel)]="editingRule.priority" min="0">
              </div>
              <div class="form-group">
                <label>Active</label>
                <input type="checkbox" [(ngModel)]="editingRule.isActive">
              </div>
            </div>

            <!-- Criteria -->
            <div class="section">
              <h3>Criteria (AND logic)</h3>
              <div class="criteria-list">
                @for (criteria of editingRule.criteria; track $index; let i = $index) {
                  <div class="criteria-row">
                    <select [(ngModel)]="criteria.field">
                      @for (field of availableFields; track field) {
                        <option [value]="field">{{ field }}</option>
                      }
                    </select>

                    <select [(ngModel)]="criteria.operator">
                      @for (op of availableOperators; track op) {
                        <option [value]="op">{{ op }}</option>
                      }
                    </select>

                    <input type="text" [(ngModel)]="criteria.value" placeholder="Value">

                    <button class="btn-remove" (click)="removeCriteria(i)">×</button>
                  </div>
                }

                <button class="btn-add" (click)="addCriteria()">+ Add Criteria</button>
              </div>
            </div>

            <!-- Actions -->
            <div class="section">
              <h3>Actions</h3>
              <div class="actions-list">
                @for (action of editingRule.actions; track $index; let i = $index) {
                  <div class="action-row">
                    <select [(ngModel)]="action.actionType">
                      @for (type of availableActionTypes; track type) {
                        <option [value]="type">{{ type }}</option>
                      }
                    </select>

                    <input type="text" [(ngModel)]="action.value" placeholder="Value (e.g., Entity GUID)">

                    <button class="btn-remove" (click)="removeAction(i)">×</button>
                  </div>
                }

                <button class="btn-add" (click)="addAction()">+ Add Action</button>
              </div>
            </div>
          </div>

          <div class="editor-footer">
            <button class="btn-secondary" (click)="cancelEdit()">Cancel</button>
            <button class="btn-primary" (click)="saveRule()">Save Rule</button>
          </div>
        </div>
      }

      <!-- Rules List -->
      <div class="rules-list">
        @for (rule of rules; track rule.id) {
          <div class="rule-card" [class.inactive]="!rule.isActive">
            <div class="rule-header">
              <div class="rule-info">
                <h3>{{ rule.name }}</h3>
                <span class="priority">Priority: {{ rule.priority }}</span>
              </div>
              <div class="rule-actions">
                <button class="btn-secondary" (click)="editRule(rule)">Edit</button>
                <button class="btn-danger" (click)="deleteRule(rule)">Delete</button>
              </div>
            </div>

            @if (rule.description) {
              <p class="rule-description">{{ rule.description }}</p>
            }

            <div class="rule-details">
              <div class="detail-section">
                <span class="label">Criteria:</span>
                <span class="value">{{ rule.criteria.length }} condition(s)</span>
              </div>
              <div class="detail-section">
                <span class="label">Actions:</span>
                <span class="value">{{ rule.actions.length }} action(s)</span>
              </div>
            </div>

            <div class="rule-criteria-preview">
              @for (criteria of rule.criteria; track $index) {
                <span class="criteria-tag">
                  {{ criteria.field }} {{ criteria.operator }} "{{ criteria.value }}"
                </span>
              }
            </div>

            <div class="rule-actions-preview">
              @for (action of rule.actions; track $index) {
                <span class="action-tag">
                  {{ action.actionType }}: {{ action.value || 'N/A' }}
                </span>
              }
            </div>
          </div>
        }

        @if (rules.length === 0) {
          <div class="empty-state">
            No import rules configured. Click "New Rule" to create one.
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .import-rule-editor {
      padding: 24px;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .header h1 {
      margin: 0;
      color: #fff;
    }

    .editor-panel {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      margin-bottom: 24px;
      overflow: hidden;
    }

    .editor-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 20px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }

    .editor-header h2 {
      margin: 0;
      color: #fff;
      font-size: 1.1rem;
    }

    .btn-close {
      background: none;
      border: none;
      color: #a0a0b0;
      font-size: 1.5rem;
      cursor: pointer;
    }

    .btn-close:hover {
      color: #fff;
    }

    .editor-body {
      padding: 20px;
    }

    .form-group {
      margin-bottom: 16px;
    }

    .form-group label {
      display: block;
      color: #a0a0b0;
      font-size: 0.85rem;
      margin-bottom: 8px;
    }

    .form-group input,
    .form-group textarea,
    .form-group select {
      width: 100%;
      background: rgba(0, 0, 0, 0.3);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 10px 12px;
      color: #fff;
      font-size: 0.9rem;
    }

    .form-group textarea {
      min-height: 80px;
      resize: vertical;
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .section {
      margin-top: 24px;
      padding-top: 24px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }

    .section h3 {
      margin: 0 0 16px 0;
      color: #fff;
      font-size: 1rem;
    }

    .criteria-row,
    .action-row {
      display: grid;
      grid-template-columns: 1fr 1fr 2fr auto;
      gap: 12px;
      margin-bottom: 12px;
      align-items: center;
    }

    .criteria-row select,
    .action-row select,
    .criteria-row input,
    .action-row input {
      background: rgba(0, 0, 0, 0.3);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 8px 12px;
      color: #fff;
      font-size: 0.85rem;
    }

    .btn-add {
      background: rgba(59, 130, 246, 0.1);
      color: #3b82f6;
      border: 1px dashed rgba(59, 130, 246, 0.3);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
    }

    .btn-add:hover {
      background: rgba(59, 130, 246, 0.2);
    }

    .btn-remove {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
      border: 1px solid rgba(239, 68, 68, 0.2);
      width: 32px;
      height: 32px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 1.1rem;
    }

    .btn-remove:hover {
      background: rgba(239, 68, 68, 0.2);
    }

    .editor-footer {
      display: flex;
      justify-content: flex-end;
      gap: 12px;
      padding: 16px 20px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }

    .rules-list {
      display: grid;
      gap: 16px;
    }

    .rule-card {
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 20px;
      transition: all 0.3s ease;
    }

    .rule-card:hover {
      border-color: rgba(255, 255, 255, 0.15);
    }

    .rule-card.inactive {
      opacity: 0.6;
    }

    .rule-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 12px;
    }

    .rule-info h3 {
      margin: 0 0 4px 0;
      color: #fff;
      font-size: 1rem;
    }

    .priority {
      color: #a0a0b0;
      font-size: 0.8rem;
    }

    .rule-actions {
      display: flex;
      gap: 8px;
    }

    .rule-description {
      color: #a0a0b0;
      font-size: 0.85rem;
      margin-bottom: 12px;
    }

    .rule-details {
      display: flex;
      gap: 24px;
      margin-bottom: 12px;
    }

    .detail-section .label {
      color: #6b7280;
      font-size: 0.8rem;
    }

    .detail-section .value {
      color: #fff;
      font-size: 0.85rem;
      margin-left: 8px;
    }

    .rule-criteria-preview,
    .rule-actions-preview {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .criteria-tag {
      background: rgba(59, 130, 246, 0.1);
      color: #3b82f6;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
    }

    .action-tag {
      background: rgba(16, 185, 129, 0.1);
      color: #10b981;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
    }

    .btn-primary {
      background: linear-gradient(135deg, #3b82f6, #8b5cf6);
      color: #fff;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 500;
    }

    .btn-primary:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
    }

    .btn-secondary {
      background: rgba(255, 255, 255, 0.05);
      color: #fff;
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
    }

    .btn-secondary:hover {
      background: rgba(255, 255, 255, 0.1);
    }

    .btn-danger {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
      border: 1px solid rgba(239, 68, 68, 0.2);
      padding: 8px 16px;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
    }

    .btn-danger:hover {
      background: rgba(239, 68, 68, 0.2);
    }

    .empty-state {
      text-align: center;
      padding: 60px;
      color: #a0a0b0;
    }
  `]
})
export class ImportRuleEditorComponent implements OnInit {
  private readonly discoveryService = inject(DiscoveryService);
  readonly store = inject(DiscoveryStore);

  rules: ImportRule[] = [];
  editingRule: ImportRule | null = null;

  availableFields: string[] = [];
  availableOperators: string[] = [];
  availableActionTypes: string[] = [];

  ngOnInit(): void {
    this.loadMetadata();
    this.loadRules();
  }

  loadMetadata(): void {
    this.discoveryService.getImportRuleFields().subscribe({
      next: (fields) => this.availableFields = fields
    });

    this.discoveryService.getImportRuleOperators().subscribe({
      next: (ops) => this.availableOperators = ops
    });

    this.discoveryService.getImportRuleActionTypes().subscribe({
      next: (types) => this.availableActionTypes = types
    });
  }

  loadRules(): void {
    this.discoveryService.getImportRules().subscribe({
      next: (rules) => this.rules = rules,
      error: (err) => console.error('Failed to load rules:', err)
    });
  }

  createNewRule(): void {
    this.editingRule = {
      id: '',
      name: '',
      description: '',
      isActive: true,
      priority: 0,
      criteria: [],
      actions: [],
      createdAt: new Date().toISOString()
    };
  }

  editRule(rule: ImportRule): void {
    this.editingRule = { ...rule, criteria: [...rule.criteria], actions: [...rule.actions] };
  }

  cancelEdit(): void {
    this.editingRule = null;
  }

  addCriteria(): void {
    if (this.editingRule) {
      this.editingRule.criteria.push({ field: '', operator: 'Contains', value: '' });
    }
  }

  removeCriteria(index: number): void {
    if (this.editingRule) {
      this.editingRule.criteria.splice(index, 1);
    }
  }

  addAction(): void {
    if (this.editingRule) {
      this.editingRule.actions.push({ actionType: 'AssignEntity', value: '' });
    }
  }

  removeAction(index: number): void {
    if (this.editingRule) {
      this.editingRule.actions.splice(index, 1);
    }
  }

  saveRule(): void {
    if (!this.editingRule) return;

    const request = {
      name: this.editingRule.name,
      description: this.editingRule.description,
      priority: this.editingRule.priority,
      criteria: this.editingRule.criteria.map(c => ({
        field: c.field,
        operator: c.operator,
        value: c.value
      })),
      actions: this.editingRule.actions.map(a => ({
        actionType: a.actionType,
        value: a.value
      }))
    };

    if (this.editingRule.id) {
      this.discoveryService.updateImportRule(this.editingRule.id, request).subscribe({
        next: () => {
          this.loadRules();
          this.cancelEdit();
        },
        error: (err) => console.error('Failed to update rule:', err)
      });
    } else {
      this.discoveryService.createImportRule(request).subscribe({
        next: () => {
          this.loadRules();
          this.cancelEdit();
        },
        error: (err) => console.error('Failed to create rule:', err)
      });
    }
  }

  deleteRule(rule: ImportRule): void {
    if (confirm(`Delete rule "${rule.name}"?`)) {
      this.discoveryService.deleteImportRule(rule.id).subscribe({
        next: () => this.loadRules(),
        error: (err) => console.error('Failed to delete rule:', err)
      });
    }
  }
}
