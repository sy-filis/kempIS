import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { Language, LanguageRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class LanguagesApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly Language[]> {
    return this.api.get<readonly Language[]>("/languages");
  }

  create(body: LanguageRequest): Observable<string> {
    return this.api.post<string>("/languages", body);
  }

  update(id: string, body: LanguageRequest): Observable<void> {
    return this.api.put<void>(`/languages/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/languages/${id}`);
  }
}
