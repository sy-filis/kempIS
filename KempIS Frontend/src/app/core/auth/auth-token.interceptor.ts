import type { HttpInterceptorFn, HttpRequest } from "@angular/common/http";
import { HttpErrorResponse } from "@angular/common/http";
import { inject } from "@angular/core";

import { catchError, from, switchMap, throwError } from "rxjs";

import { AuthService } from "./auth.service";
import { API_BASE_URL } from "../api/api-base-url.token";

export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const baseUrl = inject(API_BASE_URL);

  const isApiRequest = req.url.startsWith(baseUrl);
  const isAuthRoute = req.url.includes("/auth/");

  const initial = withAuthHeader(req, auth.accessToken(), isApiRequest);

  return next(initial).pipe(
    catchError(err => {
      if (
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        isApiRequest &&
        !isAuthRoute
      ) {
        return from(auth.refresh()).pipe(
          switchMap(() => {
            const newToken = auth.accessToken();
            if (newToken === null) {
              return throwError(() => err);
            }
            const retried = withAuthHeader(req, newToken, true);
            return next(retried);
          })
        );
      }
      return throwError(() => err);
    })
  );
};

function withAuthHeader<T>(
  req: HttpRequest<T>,
  token: string | null,
  attach: boolean
): HttpRequest<T> {
  if (!attach || token === null) {
    return req;
  }
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}
