import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CleaningPlanDetail,
  MarkCleanedRequest,
  UpdateCleanInfoRequest,
} from "./cleaning.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class CleaningApi {
  private readonly api = inject(ApiClient);

  getByDate(date: string): Observable<CleaningPlanDetail> {
    return this.api.get<CleaningPlanDetail>(`/cleaning-plans/${date}`);
  }

  /** The plan is auto-created if it doesn't exist yet. */
  addCleanInfo(date: string, spotId: string): Observable<string> {
    return this.api.post<string>(`/cleaning-plans/${date}/clean-infos`, {
      spotId,
    });
  }

  deleteCleanInfo(id: string): Observable<void> {
    return this.api.delete<void>(`/clean-infos/${id}`);
  }

  /** Backend leaves the existing note untouched if `note` is null; pass
   *  an empty string to clear it. */
  updateNote(id: string, body: UpdateCleanInfoRequest): Observable<void> {
    return this.api.patch<void>(`/clean-infos/${id}`, body);
  }

  /** One-way: backend rejects re-marking an already-cleaned entry with
   *  a 409. */
  markCleaned(id: string, body?: MarkCleanedRequest): Observable<void> {
    return this.api.post<void>(`/clean-infos/${id}/mark-cleaned`, body ?? null);
  }
}
