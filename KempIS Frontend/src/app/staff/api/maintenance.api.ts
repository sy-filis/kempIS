import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

import type {
  CreateMaintenanceIssueRequest,
  MaintenanceIssue,
  UpdateMaintenanceIssueRequest,
} from "./maintenance.types";
import { ApiClient } from "../../core/api/api-client";

@Injectable({ providedIn: "root" })
export class MaintenanceApi {
  private readonly api = inject(ApiClient);

  get(id: string): Observable<MaintenanceIssue> {
    return this.api.get<MaintenanceIssue>(`/maintenance-issues/${id}`);
  }

  create(body: CreateMaintenanceIssueRequest): Observable<MaintenanceIssue> {
    return this.api.post<MaintenanceIssue>("/maintenance-issues", body);
  }

  update(
    id: string,
    body: UpdateMaintenanceIssueRequest
  ): Observable<MaintenanceIssue> {
    return this.api.patch<MaintenanceIssue>(`/maintenance-issues/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`/maintenance-issues/${id}`);
  }
}
