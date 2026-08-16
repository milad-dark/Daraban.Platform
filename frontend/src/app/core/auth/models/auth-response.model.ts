export interface AuthResponse {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface ApiError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}