import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest } from './models/login-request.model';
import { RegisterRequest } from './models/register-request.model';
import { AuthResponse } from './models/auth-response.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  // Matches the backend contract exactly: AuthController serves api/v1/identity/auth/*.
  // The bare /v1/auth path this used to call exists on no controller and returned 404,
  // which meant login/register/refresh/logout all failed against the real backend.
  // (The backend's module-namespaced api/v1/{module}/... convention plus the tested
  // contract in PermissionEnforcementTests both confirm the identity/ segment belongs.)
  private readonly baseUrl = `${environment.apiUrl}/v1/identity/auth`;

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.baseUrl}/login`,
      request,
      { withCredentials: true }
    );
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.baseUrl}/register`,
      request,
      { withCredentials: true }
    );
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.baseUrl}/refresh`,
      {},
      { withCredentials: true }
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/logout`,
      {},
      { withCredentials: true }
    );
  }
}