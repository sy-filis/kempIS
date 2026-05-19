import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";
import { ProgressBarModule } from "primeng/progressbar";
import { TagModule } from "primeng/tag";

import { ApiClient } from "../../../core/api/api-client";
import { AuthService } from "../../../core/auth/auth.service";
import { Roles } from "../../../core/auth/roles";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import { CleaningApi } from "../../api/cleaning.api";
import type { CleaningPlanDetail } from "../../api/cleaning.types";
import type { Spot, SpotGroup, SpotStateRecord } from "../../api/spots.types";
import { SpotState } from "../../api/spots.types";
import { ScreenHeader } from "../shared/screen-header";

type CottageRow = {
  readonly cleanInfoId: string;
  readonly spotId: string;
  readonly spotName: string;
  readonly groupName: string;
  readonly occupied: boolean;
  readonly done: boolean;
  readonly note: string | null;
};

type CottageGroup = {
  readonly type: string;
  readonly items: readonly CottageRow[];
  readonly done: number;
  readonly total: number;
};

const LOCALE = "cs-CZ";

@Component({
  selector: "kemp-is-staff-cleaning",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    ProgressBarModule,
    TagModule,
    ScreenHeader,
  ],
  templateUrl: "./cleaning.page.html",
  styleUrl: "./cleaning.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleaningPage {
  private readonly apiClient = inject(ApiClient);
  private readonly cleaningApi = inject(CleaningApi);
  private readonly auth = inject(AuthService);

  protected readonly canAckCleaning = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.CleaningStaff)
  );

  protected readonly selectedDate = signal<string>(dateToIso(new Date()));

  protected readonly spots = httpResource<readonly Spot[]>(() =>
    this.apiClient.url("/spots")
  );

  protected readonly spotGroups = httpResource<readonly SpotGroup[]>(() =>
    this.apiClient.url("/spot-groups")
  );

  protected readonly spotStates = httpResource<readonly SpotStateRecord[]>(() =>
    this.apiClient.url("/spots/states")
  );

  protected readonly planForDay = httpResource<CleaningPlanDetail>(() =>
    this.apiClient.url(`/cleaning-plans/${this.selectedDate()}`)
  );

  // Tracks in-flight mark-cleaned requests; disables the button to prevent overlapping POSTs.
  private readonly pendingMark = signal<ReadonlySet<string>>(new Set());

  protected readonly pickerDate = computed(() =>
    isoToDate(this.selectedDate())
  );

  protected readonly cottageRows = computed<readonly CottageRow[]>(() => {
    if (!this.planForDay.hasValue()) {
      return [];
    }
    const spotById = new Map(
      (this.spots.hasValue() ? this.spots.value() : []).map(s => [s.id, s])
    );
    const groupById = new Map(
      (this.spotGroups.hasValue() ? this.spotGroups.value() : []).map(g => [
        g.id,
        g,
      ])
    );
    const stateBySpotId = new Map(
      (this.spotStates.hasValue() ? this.spotStates.value() : []).map(s => [
        s.spotId,
        s,
      ])
    );

    return this.planForDay.value().cleanInfos.map(ci => {
      const spot = spotById.get(ci.spotId);
      const group = spot ? groupById.get(spot.spotGroupId) : undefined;
      const stateRec = stateBySpotId.get(ci.spotId);
      const apiDone = ci.completedAtUtc !== null && ci.completedAtUtc > 0;
      return {
        cleanInfoId: ci.id,
        spotId: ci.spotId,
        spotName: spot?.name ?? "-",
        groupName: group?.name ?? "Ostatní",
        occupied:
          stateRec?.state === SpotState.Occupied ||
          stateRec?.state === SpotState.ExpectingDeparture,
        done: apiDone,
        note: ci.note,
      };
    });
  });

  protected readonly groups = computed<readonly CottageGroup[]>(() => {
    const map = new Map<string, CottageRow[]>();
    for (const row of this.cottageRows()) {
      const list = map.get(row.groupName) ?? [];
      list.push(row);
      map.set(row.groupName, list);
    }
    return [...map.entries()].map(([type, items]) => ({
      type,
      items: [...items].sort((a, b) =>
        a.spotName.localeCompare(b.spotName, LOCALE, { numeric: true })
      ),
      done: items.filter(r => r.done).length,
      total: items.length,
    }));
  });

  protected readonly totalDone = computed(
    () => this.cottageRows().filter(r => r.done).length
  );

  protected readonly totalCount = computed(() => this.cottageRows().length);

  protected readonly progressPercent = computed(() =>
    this.totalCount() === 0
      ? 0
      : Math.round((this.totalDone() / this.totalCount()) * 100)
  );

  protected readonly isEmpty = computed(
    () => this.planForDay.hasValue() && this.totalCount() === 0
  );

  protected readonly subtitle = computed(() => {
    if (!this.planForDay.hasValue()) {
      return "Načítá se…";
    }
    if (this.totalCount() === 0) {
      return "Žádné úklidy";
    }
    return `${this.totalDone()} z ${this.totalCount()} hotovo`;
  });

  protected onDateChange(value: Date | null): void {
    if (!value) {
      return;
    }
    this.selectedDate.set(dateToIso(value));
  }

  protected isPending(cleanInfoId: string): boolean {
    return this.pendingMark().has(cleanInfoId);
  }

  // One-way endpoint; an already-cleaned entry cannot be reverted from the UI.
  protected markCleaned(cleanInfoId: string, currentlyDone: boolean): void {
    if (currentlyDone) {
      return;
    }
    if (this.pendingMark().has(cleanInfoId)) {
      return;
    }
    this.setPending(cleanInfoId, true);
    this.cleaningApi.markCleaned(cleanInfoId).subscribe({
      next: () => {
        this.setPending(cleanInfoId, false);
        this.planForDay.reload();
      },
      error: () => this.setPending(cleanInfoId, false),
    });
  }

  private setPending(cleanInfoId: string, pending: boolean): void {
    this.pendingMark.update(set => {
      const next = new Set(set);
      if (pending) {
        next.add(cleanInfoId);
      } else {
        next.delete(cleanInfoId);
      }
      return next;
    });
  }
}
