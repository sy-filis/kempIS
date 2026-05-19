import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type { Nationality, NationalityRequest } from "../shared/types";

@Injectable({ providedIn: "root" })
export class NationalitiesApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly Nationality[]> {
    return this.api.get<readonly Nationality[]>("/nationalities");
  }

  create(body: NationalityRequest): Observable<string> {
    return this.api.post<string>("/nationalities", body);
  }

  update(id: string, body: NationalityRequest): Observable<void> {
    return this.api.put<void>(`/nationalities/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/nationalities/${id}`);
  }
}
