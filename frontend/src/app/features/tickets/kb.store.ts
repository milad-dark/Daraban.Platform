import { inject, computed, signal } from '@angular/core';
import { signalStore, withState, withMethods, withComputed, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { extractError } from '../../core/utils/error.util';
import { KbService } from './kb.service';
import {
  KbArticle,
  KbArticleListItem,
  KbArticleSearchHit,
  KbCategory,
} from './models/ticket.models';

interface KbState {
  articles: KbArticleListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  selectedArticle: KbArticle | null;
  categories: KbCategory[];
  searchQuery: string;
  searchResults: KbArticleSearchHit[];
  isSearching: boolean;
  isLoading: boolean;
  isLoadingDetail: boolean;
  error: string | null;
}

const initialState: KbState = {
  articles: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  selectedArticle: null,
  categories: [],
  searchQuery: '',
  searchResults: [],
  isSearching: false,
  isLoading: false,
  isLoadingDetail: false,
  error: null,
};

export const KbStore = signalStore(
  { providedIn: 'root' },

  withState<KbState>(initialState),

  withComputed((store) => ({
    hasResults: computed(() => store.searchResults().length > 0),
    publishedArticles: computed(() => store.articles().filter((a) => a.status === 2)),
  })),

  withMethods((store, kbService = inject(KbService)) => ({
    async loadCategories(): Promise<void> {
      try {
        const categories = await firstValueFrom(kbService.getCategories());
        patchState(store, { categories });
      } catch {
        // Non-critical.
      }
    },

    async loadArticles(page = 1): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const result = await firstValueFrom(kbService.getArticles({ page, pageSize: store.pageSize() }));
        patchState(store, {
          articles: result.items,
          totalCount: result.totalCount,
          page: result.page,
          isLoading: false,
        });
      } catch (err: unknown) {
        patchState(store, { isLoading: false, error: extractError(err) });
      }
    },

    async loadArticle(id: string, countView = true): Promise<void> {
      patchState(store, { isLoadingDetail: true, error: null });
      try {
        const article = await firstValueFrom(kbService.getById(id, countView));
        patchState(store, { selectedArticle: article, isLoadingDetail: false });
      } catch (err: unknown) {
        patchState(store, { isLoadingDetail: false, error: extractError(err) });
      }
    },

    clearSelectedArticle(): void {
      patchState(store, { selectedArticle: null });
    },

    async search(query: string): Promise<void> {
      patchState(store, { searchQuery: query, isSearching: true, error: null });
      if (!query.trim()) {
        patchState(store, { searchResults: [], isSearching: false });
        return;
      }
      try {
        const result = await firstValueFrom(kbService.search(query));
        patchState(store, { searchResults: result.items, isSearching: false });
      } catch (err: unknown) {
        patchState(store, { isSearching: false, error: extractError(err) });
      }
    },

    clearSearch(): void {
      patchState(store, { searchQuery: '', searchResults: [], isSearching: false });
    },

    async submitFeedback(articleId: string, isHelpful: boolean, comment?: string): Promise<boolean> {
      try {
        const summary = await firstValueFrom(kbService.submitFeedback(articleId, isHelpful, comment));
        const current = store.selectedArticle();
        if (current && current.id === articleId) {
          patchState(store, {
            selectedArticle: {
              ...current,
              helpfulCount: summary.helpfulCount,
              notHelpfulCount: summary.notHelpfulCount,
            },
          });
        }
        return true;
      } catch (err: unknown) {
        patchState(store, { error: extractError(err) });
        return false;
      }
    },

    clearError(): void {
      patchState(store, { error: null });
    },
  }))
);
