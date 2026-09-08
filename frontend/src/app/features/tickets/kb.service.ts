import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  KbCategory,
  KbArticle,
  KbArticleSearchResult,
  KbFeedbackSummary,
} from './models/ticket.models';

@Injectable({ providedIn: 'root' })
export class KbService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/v1/kb/articles`;
  private readonly categoriesUrl = `${environment.apiUrl}/v1/kb/categories`;

  getCategories(): Observable<KbCategory[]> {
    return this.http.get<KbCategory[]>(this.categoriesUrl);
  }

  getArticles(filters: {
    categoryId?: string;
    isFaq?: boolean;
    title?: string;
    page?: number;
    pageSize?: number;
  }): Observable<{ items: KbArticle[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams()
      .set('page', (filters.page ?? 1).toString())
      .set('pageSize', (filters.pageSize ?? 20).toString());

    if (filters.categoryId) params = params.set('categoryId', filters.categoryId);
    if (filters.isFaq != null) params = params.set('isFaq', filters.isFaq.toString());
    if (filters.title) params = params.set('title', filters.title);

    return this.http.get<{ items: KbArticle[]; totalCount: number; page: number; pageSize: number }>(
      this.baseUrl,
      { params }
    );
  }

  search(query: string, page = 1, pageSize = 10): Observable<KbArticleSearchResult> {
    const params = new HttpParams()
      .set('q', query)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<KbArticleSearchResult>(`${this.baseUrl}/search`, { params });
  }

  getById(id: string, countView = false): Observable<KbArticle> {
    const params = new HttpParams().set('countView', countView.toString());
    return this.http.get<KbArticle>(`${this.baseUrl}/${id}`, { params });
  }

  submitFeedback(id: string, isHelpful: boolean, comment?: string): Observable<KbFeedbackSummary> {
    return this.http.post<KbFeedbackSummary>(`${this.baseUrl}/${id}/feedback`, {
      isHelpful,
      comment: comment ?? null,
    });
  }
}
