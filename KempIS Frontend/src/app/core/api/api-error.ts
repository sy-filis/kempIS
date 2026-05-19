export type ApiValidationError = {
  code: string;
  description: string;
};

export type ApiError = {
  status: number;
  code: string;
  detail: string;
  validationErrors?: readonly ApiValidationError[];
};

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === "object" &&
    value !== null &&
    "status" in value &&
    "code" in value &&
    "detail" in value
  );
}
