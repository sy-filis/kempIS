import type { HttpInterceptorFn } from "@angular/common/http";
import { HttpErrorResponse } from "@angular/common/http";

import { catchError, throwError } from "rxjs";

import type { ApiError, ApiValidationError } from "./api-error";

type Rfc7807 = {
  title?: string;
  detail?: string;
  status?: number;
  errors?: readonly ApiValidationError[];
};

function isRfc7807(value: unknown): value is Rfc7807 {
  return (
    typeof value === "object" &&
    value !== null &&
    ("title" in value || "detail" in value || "status" in value)
  );
}

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((response: unknown) => {
      const apiError = toApiError(response);
      return throwError(() => apiError);
    })
  );

function toApiError(response: unknown): ApiError {
  if (!(response instanceof HttpErrorResponse)) {
    return { status: 0, code: "Network", detail: "Unexpected error" };
  }

  const body = response.error;

  if (isRfc7807(body)) {
    return {
      status: body.status ?? response.status,
      code: body.title ?? "Unknown",
      detail: body.detail ?? response.statusText,
      validationErrors: body.errors,
    };
  }

  return {
    status: response.status,
    code: "Network",
    detail:
      typeof body === "string" && body.length > 0 ? body : response.message,
  };
}
