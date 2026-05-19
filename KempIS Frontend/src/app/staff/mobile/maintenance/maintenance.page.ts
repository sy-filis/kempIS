import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";
import { TagModule } from "primeng/tag";
import { firstValueFrom } from "rxjs";

import { ApiClient } from "../../../core/api/api-client";
import { UsersStore } from "../../../core/users/users.store";
import type { MaintenanceIssue } from "../../api/maintenance.types";
import type { Spot } from "../../api/spots.types";
import { ScreenHeader } from "../shared/screen-header";

type RequestStatus = "open" | "closed";

type FilterId = "all" | "open" | "closed";

type RequestRow = {
  readonly id: string;
  readonly title: string;
  readonly status: RequestStatus;
  readonly reported: string | null;
  readonly spotName: string | null;
  readonly note: string | null;
  readonly resolved: string | null;
  readonly solverName: string | null;
};

const LOCALE = "cs-CZ";
const DAY_MS = 86_400_000;

@Component({
  selector: "kemp-is-staff-maintenance",
  imports: [ButtonModule, TagModule, ScreenHeader],
  templateUrl: "./maintenance.page.html",
  styleUrl: "./maintenance.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MaintenancePage {
  private readonly apiClient = inject(ApiClient);
  private readonly usersStore = inject(UsersStore);

  protected readonly issues = httpResource<readonly MaintenanceIssue[]>(() =>
    this.apiClient.url("/maintenance-issues")
  );

  protected readonly spots = httpResource<readonly Spot[]>(() =>
    this.apiClient.url("/spots")
  );

  protected readonly activeFilter = signal<FilterId>("all");
  protected readonly resolving = signal<ReadonlySet<string>>(new Set());

  protected readonly requests = computed<readonly RequestRow[]>(() => {
    if (!this.issues.hasValue()) {
      return [];
    }
    const spotById = new Map(
      (this.spots.hasValue() ? this.spots.value() : []).map(s => [s.id, s])
    );
    return this.issues.value().map(issue => {
      const spot = issue.spotId ? spotById.get(issue.spotId) : undefined;
      return {
        id: issue.id,
        title: issue.problemDescription,
        status: issue.resolvedAtUtc ? "closed" : "open",
        reported: this.formatRelativeDateTime(issue.issuedAtUtc),
        spotName: spot?.name ?? null,
        note: issue.note,
        resolved: this.formatRelativeDateTime(issue.resolvedAtUtc),
        solverName: this.usersStore.name(issue.solverUserId),
      } satisfies RequestRow;
    });
  });

  protected readonly openRequests = computed(() =>
    this.requests().filter(r => r.status === "open")
  );

  protected readonly closedRequests = computed(() =>
    this.requests().filter(r => r.status === "closed")
  );

  protected readonly filters = computed<
    readonly {
      readonly id: FilterId;
      readonly label: string;
      readonly count: number;
    }[]
  >(() => [
    { id: "all", label: "Vše", count: this.requests().length },
    { id: "open", label: "Otevřené", count: this.openRequests().length },
    { id: "closed", label: "Uzavřené", count: this.closedRequests().length },
  ]);

  protected readonly subtitle = computed(() => {
    if (!this.issues.hasValue()) {
      return "Načítá se…";
    }
    const count = this.openRequests().length;
    if (count === 0) {
      return "Žádné otevřené požadavky";
    }
    return `${count} otevřených požadavků`;
  });

  protected setFilter(id: FilterId): void {
    this.activeFilter.set(id);
  }

  protected isResolving(id: string): boolean {
    return this.resolving().has(id);
  }

  protected async closeRequest(id: string): Promise<void> {
    if (this.resolving().has(id)) {
      return;
    }
    this.resolving.update(set => {
      const next = new Set(set);
      next.add(id);
      return next;
    });
    try {
      await firstValueFrom(
        this.apiClient.post<void>(`/maintenance-issues/${id}/resolve`, null)
      );
      this.issues.reload();
    } finally {
      this.resolving.update(set => {
        const next = new Set(set);
        next.delete(id);
        return next;
      });
    }
  }

  private formatRelativeDateTime(epochMs: number | null): string | null {
    if (!epochMs) {
      return null;
    }
    const d = new Date(epochMs);
    if (Number.isNaN(d.getTime())) {
      return null;
    }
    const time = d.toLocaleTimeString(LOCALE, {
      hour: "2-digit",
      minute: "2-digit",
    });
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const target = new Date(d);
    target.setHours(0, 0, 0, 0);
    const diffDays = Math.round((target.getTime() - today.getTime()) / DAY_MS);
    if (diffDays === 0) {
      return `dnes ${time}`;
    }
    if (diffDays === -1) {
      return `včera ${time}`;
    }
    if (diffDays < -1 && diffDays >= -6) {
      const weekdayRaw = d.toLocaleDateString(LOCALE, { weekday: "short" });
      const weekday = weekdayRaw.replace(".", "");
      return `${weekday} ${d.getDate()}. ${d.getMonth() + 1}.`;
    }
    return `${d.getDate()}. ${d.getMonth() + 1}.`;
  }
}
