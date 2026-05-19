import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { MessageService } from "primeng/api";
import { ToastModule } from "primeng/toast";
import { forkJoin, of, switchMap } from "rxjs";

import { type CleaningRow, CleaningTable } from "./cleaning-table";
import { CleaningToolbar } from "./cleaning-toolbar";
import { MaintenanceSection } from "./maintenance-section";
import { ApiClient } from "../../../core/api/api-client";
import type { User } from "../../../core/users/users.types";
import { dateToIso } from "../../../shared/date-iso";
import { CleaningApi } from "../../api/cleaning.api";
import type { CleaningPlanDetail } from "../../api/cleaning.types";
import {
  type Reservation,
  ReservationState,
} from "../../api/reservations.types";
import type { Spot, SpotGroup } from "../../api/spots.types";

@Component({
  selector: "kemp-is-operations",
  imports: [CleaningToolbar, CleaningTable, MaintenanceSection, ToastModule],
  templateUrl: "./operations.page.html",
  styleUrl: "./operations.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class OperationsPage {
  private readonly apiClient = inject(ApiClient);
  private readonly cleaningApi = inject(CleaningApi);
  private readonly messages = inject(MessageService);

  protected readonly selectedDate = signal<string>(dateToIso(new Date()));

  // Disables the checkbox while a POST/DELETE is in flight to avoid overlapping requests.
  private readonly pendingShouldClean = signal<ReadonlySet<string>>(new Set());

  protected readonly spotGroups = httpResource<readonly SpotGroup[]>(() =>
    this.apiClient.url("/spot-groups")
  );

  protected readonly spots = httpResource<readonly Spot[]>(() =>
    this.apiClient.url("/spots")
  );

  protected readonly planForDay = httpResource<CleaningPlanDetail>(() =>
    this.apiClient.url(`/cleaning-plans/${this.selectedDate()}`)
  );

  protected readonly users = httpResource<readonly User[]>(() =>
    this.apiClient.url("/users")
  );

  private readonly userNamesById = computed<ReadonlyMap<string, string>>(() => {
    if (!this.users.hasValue()) {
      return new Map();
    }
    return new Map(this.users.value().map(u => [u.id, u.name]));
  });

  protected readonly lastUpdatedLabel = computed<string | null>(() => {
    if (!this.planForDay.hasValue()) {
      return null;
    }
    const plan = this.planForDay.value();
    const when = new Date(plan.updatedAtUtc).toLocaleString("cs-CZ", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
    const who = this.userNamesById().get(plan.updatedByUserId) ?? "—";
    return `${when} · ${who}`;
  });

  protected readonly prefilling = signal<boolean>(false);

  protected readonly cleaningRows = computed<readonly CleaningRow[]>(() => {
    const groups = this.spotGroups.hasValue() ? this.spotGroups.value() : [];
    const spots = this.spots.hasValue() ? this.spots.value() : [];
    const infos = this.planForDay.hasValue()
      ? this.planForDay.value().cleanInfos
      : [];
    const infoBySpot = new Map(infos.map(i => [i.spotId, i]));
    const result: CleaningRow[] = [];
    for (const g of groups.filter(g => g.isActive)) {
      const groupSpots = spots
        .filter(s => s.spotGroupId === g.id && s.isActive)
        .sort((a, b) => a.name.localeCompare(b.name, "cs", { numeric: true }));
      for (const s of groupSpots) {
        const info = infoBySpot.get(s.id) ?? null;
        result.push({
          spotId: s.id,
          spotName: s.name,
          groupId: g.id,
          groupName: g.name,
          cleanInfoId: info?.id ?? null,
          shouldClean: info !== null,
          done:
            info !== null &&
            info.completedAtUtc !== null &&
            info.completedAtUtc !== 0,
          note: info?.note ?? "",
        });
      }
    }
    return result;
  });

  protected readonly cleaningTotals = computed(() => {
    const rows = this.cleaningRows();
    const total = rows.filter(r => r.shouldClean).length;
    const done = rows.filter(r => r.shouldClean && r.done).length;
    return { total, done };
  });

  protected readonly disabledShouldClean = computed(() =>
    this.pendingShouldClean()
  );

  protected onShouldCleanChange(spotId: string, value: boolean): void {
    if (this.pendingShouldClean().has(spotId)) {
      return;
    }
    this.markPending(spotId, true);

    if (value) {
      this.cleaningApi.addCleanInfo(this.selectedDate(), spotId).subscribe({
        next: () => {
          this.markPending(spotId, false);
          this.planForDay.reload();
        },
        error: () => this.markPending(spotId, false),
      });
    } else {
      const info = this.findInfoForSpot(spotId);
      if (!info) {
        this.markPending(spotId, false);
        return;
      }
      this.cleaningApi.deleteCleanInfo(info.id).subscribe({
        next: () => {
          this.markPending(spotId, false);
          this.planForDay.reload();
        },
        error: () => this.markPending(spotId, false),
      });
    }
  }

  protected onNoteCommit(cleanInfoId: string, note: string): void {
    this.cleaningApi.updateNote(cleanInfoId, { note }).subscribe({
      next: () => this.planForDay.reload(),
    });
  }

  protected onPrefill(): void {
    if (this.prefilling()) {
      return;
    }
    const date = this.selectedDate();
    const existingSpotIds = new Set(
      this.planForDay.hasValue()
        ? this.planForDay.value().cleanInfos.map(c => c.spotId)
        : []
    );

    this.prefilling.set(true);
    this.apiClient
      .get<readonly Reservation[]>("/reservations", {
        params: { from: date, to: date },
      })
      .pipe(
        switchMap(reservations => {
          const toAdd = new Set<string>();
          for (const r of reservations) {
            if (r.state === ReservationState.Cancelled) {
              continue;
            }
            if (r.to !== date) {
              continue;
            }
            for (const spotId of r.spotItems) {
              if (!existingSpotIds.has(spotId)) {
                toAdd.add(spotId);
              }
            }
          }
          if (toAdd.size === 0) {
            return of(0);
          }
          return forkJoin(
            Array.from(toAdd).map(spotId =>
              this.cleaningApi.addCleanInfo(date, spotId)
            )
          ).pipe(switchMap(results => of(results.length)));
        })
      )
      .subscribe({
        next: added => {
          this.prefilling.set(false);
          this.planForDay.reload();
          if (added === 0) {
            this.messages.add({
              severity: "info",
              summary: "Předvyplnění",
              detail: "Nic k přidání – plán už obsahuje všechny odjezdy.",
            });
          } else {
            this.messages.add({
              severity: "success",
              summary: "Předvyplnění",
              detail: `Přidáno ${added} chat k úklidu.`,
            });
          }
        },
        error: () => {
          this.prefilling.set(false);
          this.planForDay.reload();
          this.messages.add({
            severity: "error",
            summary: "Předvyplnění",
            detail: "Plán se nepodařilo předvyplnit.",
          });
        },
      });
  }

  protected onDateChange(iso: string): void {
    this.selectedDate.set(iso);
  }

  private markPending(spotId: string, pending: boolean): void {
    this.pendingShouldClean.update(set => {
      const next = new Set(set);
      if (pending) {
        next.add(spotId);
      } else {
        next.delete(spotId);
      }
      return next;
    });
  }

  private findInfoForSpot(spotId: string): { id: string } | null {
    if (!this.planForDay.hasValue()) {
      return null;
    }
    return (
      this.planForDay.value().cleanInfos.find(ci => ci.spotId === spotId) ??
      null
    );
  }
}
