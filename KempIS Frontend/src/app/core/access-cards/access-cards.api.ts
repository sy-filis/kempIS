import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  AccessCard,
  IssueAccessCardRequest,
  UpdateAccessCardRequest,
} from "./access-cards.types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class AccessCardsApi {
  private readonly api = inject(ApiClient);

  list(): Observable<readonly AccessCard[]> {
    return this.api.get<readonly AccessCard[]>("/access-cards");
  }

  issue(body: IssueAccessCardRequest): Observable<AccessCard> {
    return this.api.post<AccessCard>("/access-cards", body);
  }

  update(id: string, body: UpdateAccessCardRequest): Observable<AccessCard> {
    return this.api.patch<AccessCard>(`/access-cards/${id}`, body);
  }

  returnCard(id: string): Observable<void> {
    return this.api.delete<void>(`/access-cards/${id}`);
  }
}
