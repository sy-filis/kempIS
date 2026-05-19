import type { HttpParams } from "@angular/common/http";
import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { API_BASE_URL } from "./api-base-url.token";

export type ApiRequestOptions = {
  params?: HttpParams | Record<string, string | number>;
};

export type ApiBodyOptions = ApiRequestOptions & {
  body?: unknown;
};

@Injectable({ providedIn: "root" })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  get<T>(path: string, options?: ApiRequestOptions): Observable<T> {
    return this.http.get<T>(this.url(path), options);
  }

  post<T>(
    path: string,
    body: unknown,
    options?: ApiRequestOptions
  ): Observable<T> {
    return this.http.post<T>(this.url(path), body, options);
  }

  put<T>(
    path: string,
    body: unknown,
    options?: ApiRequestOptions
  ): Observable<T> {
    return this.http.put<T>(this.url(path), body, options);
  }

  patch<T>(
    path: string,
    body: unknown,
    options?: ApiRequestOptions
  ): Observable<T> {
    return this.http.patch<T>(this.url(path), body, options);
  }

  delete<T>(path: string, options?: ApiBodyOptions): Observable<T> {
    return this.http.request<T>("DELETE", this.url(path), options);
  }

  /** Used by httpResource() in components, where the URL must be a string. */
  url(path: string): string {
    return this.URLBuilder(path).href;
  }

  URLBuilder(path: string): URL {
    return new URL(`/api${path}`, this.baseUrl);
  }
}
