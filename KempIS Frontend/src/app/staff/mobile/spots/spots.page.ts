import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";

import { ButtonModule } from "primeng/button";

import { ApiClient } from "../../../core/api/api-client";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import type { CleaningPlanDetail } from "../../api/cleaning.types";
import type {
  Spot as ApiSpot,
  SpotGroup as ApiSpotGroup,
  SpotStateRecord,
} from "../../api/spots.types";
import { SpotState as ApiSpotState } from "../../api/spots.types";
import {
  mapApiSpotStateToUi,
  SPOT_STATE_CONFIGS,
  SPOT_STATES,
  type SpotState,
  type SpotStateConfig,
} from "../../shared/spot-state";
import { ScreenHeader } from "../shared/screen-header";

type SpotTile = {
  readonly id: string;
  readonly s: SpotState;
  readonly until?: string;
  readonly clean?: boolean;
};

type SpotGroupView = {
  readonly type: string;
  readonly items: readonly SpotTile[];
};

const LOCALE = "cs-CZ";

@Component({
  selector: "kemp-is-staff-spots",
  imports: [ButtonModule, ScreenHeader],
  templateUrl: "./spots.page.html",
  styleUrl: "./spots.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SpotsPage {
  private readonly apiClient = inject(ApiClient);

  protected readonly states: readonly SpotStateConfig[] = SPOT_STATES.map(
    id => SPOT_STATE_CONFIGS[id]
  );

  protected readonly spots = httpResource<readonly ApiSpot[]>(() =>
    this.apiClient.url("/spots")
  );

  protected readonly spotGroups = httpResource<readonly ApiSpotGroup[]>(() =>
    this.apiClient.url("/spot-groups")
  );

  protected readonly spotStates = httpResource<readonly SpotStateRecord[]>(() =>
    this.apiClient.url("/spots/states")
  );

  private readonly today = dateToIso(new Date());

  private readonly todayCleaningPlan = httpResource<CleaningPlanDetail>(() =>
    this.apiClient.url(`/cleaning-plans/${this.today}`)
  );

  private readonly pendingCleaningSpotIds = computed<ReadonlySet<string>>(
    () => {
      if (!this.todayCleaningPlan.hasValue()) {
        return new Set();
      }
      const ids = new Set<string>();
      for (const ci of this.todayCleaningPlan.value().cleanInfos) {
        if (ci.completedAtUtc === null || ci.completedAtUtc === 0) {
          ids.add(ci.spotId);
        }
      }
      return ids;
    }
  );

  private readonly lastRefresh = signal<number>(Date.now());

  protected readonly subtitle = computed(() => {
    if (!this.spots.hasValue() || !this.spotStates.hasValue()) {
      return "Načítá se…";
    }
    const d = new Date(this.lastRefresh());
    const weekdayRaw = d.toLocaleDateString(LOCALE, { weekday: "long" });
    const weekday = weekdayRaw.charAt(0).toUpperCase() + weekdayRaw.slice(1);
    const time = d.toLocaleTimeString(LOCALE, {
      hour: "2-digit",
      minute: "2-digit",
    });
    return `${weekday} ${d.getDate()}. ${d.getMonth() + 1}. · ${time}`;
  });

  protected readonly groups = computed<readonly SpotGroupView[]>(() => {
    if (!this.spots.hasValue() || !this.spotGroups.hasValue()) {
      return [];
    }
    const stateBySpotId = new Map(
      (this.spotStates.hasValue() ? this.spotStates.value() : []).map(s => [
        s.spotId,
        s,
      ])
    );
    const pendingClean = this.pendingCleaningSpotIds();

    const tilesByGroupId = new Map<string, SpotTile[]>();
    for (const apiSpot of this.spots.value()) {
      if (!apiSpot.isActive) {
        continue;
      }
      const stateRec = stateBySpotId.get(apiSpot.id);
      const tile: SpotTile = {
        id: apiSpot.name,
        s: mapApiSpotStateToUi(stateRec?.state ?? ApiSpotState.Unoccupied),
        until: this.formatShortDate(stateRec?.departureDate ?? null),
        clean: !pendingClean.has(apiSpot.id),
      };
      const list = tilesByGroupId.get(apiSpot.spotGroupId) ?? [];
      list.push(tile);
      tilesByGroupId.set(apiSpot.spotGroupId, list);
    }

    return this.spotGroups
      .value()
      .filter(g => g.isActive)
      .map(g => ({
        type: g.name,
        items: (tilesByGroupId.get(g.id) ?? []).sort((a, b) =>
          a.id.localeCompare(b.id, LOCALE, { numeric: true })
        ),
      }))
      .filter(g => g.items.length > 0);
  });

  protected readonly counts = computed<Record<SpotState, number>>(() => {
    const acc: Record<SpotState, number> = {
      free: 0,
      arrival: 0,
      departure: 0,
      occupied: 0,
      ooo: 0,
    };
    for (const group of this.groups()) {
      for (const item of group.items) {
        acc[item.s]++;
      }
    }
    return acc;
  });

  protected stateInfo(id: SpotState): SpotStateConfig {
    return SPOT_STATE_CONFIGS[id];
  }

  protected needsClean(spot: SpotTile): boolean {
    return spot.clean === false;
  }

  protected onRefresh(): void {
    this.spots.reload();
    this.spotGroups.reload();
    this.spotStates.reload();
    this.todayCleaningPlan.reload();
    this.lastRefresh.set(Date.now());
  }

  private formatShortDate(iso: string | null): string | undefined {
    if (!iso) {
      return undefined;
    }
    const d = isoToDate(iso);
    if (!d) {
      return undefined;
    }
    return `${d.getDate()}. ${d.getMonth() + 1}.`;
  }
}
