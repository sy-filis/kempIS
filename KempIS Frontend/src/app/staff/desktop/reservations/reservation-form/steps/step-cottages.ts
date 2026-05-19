import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model,
  ViewEncapsulation,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";

import type { Spot, SpotGroup } from "../../../../api/spots.types";

export type SpotRow = {
  readonly id: string;
  readonly spotGroupId: string | null;
  readonly spotId: string | null;
  // Carried from the server detail for display; not editable here.
  readonly hasGivenKey?: boolean;
  readonly hasReturnedKeys?: boolean;
};

export type KeyState = "out" | "returned" | "pending";

type SpotView = {
  readonly spot: Spot;
  readonly selected: boolean;
  readonly rowId: string | null;
};

type GroupView = {
  readonly group: SpotGroup;
  readonly spots: readonly SpotView[];
  readonly selectedCount: number;
  readonly availableCount: number;
  readonly requestedCount: number;
  // Negative = still missing, positive = over-assigned, 0 = OK.
  readonly mismatch: number;
};

type SelectedView = {
  readonly row: SpotRow;
  readonly group: SpotGroup | null;
  readonly spot: Spot | null;
  readonly keyState: KeyState;
};

export function keyStateOf(
  hasGiven: boolean | undefined,
  hasReturned: boolean | undefined
): KeyState {
  if (hasReturned) {
    return "returned";
  }
  if (hasGiven) {
    return "out";
  }
  return "pending";
}

type RequestSummary = {
  readonly totalRequested: number;
  readonly totalAssigned: number;
  readonly groups: readonly {
    readonly group: SpotGroup;
    readonly requested: number;
    readonly assigned: number;
    readonly mismatch: number;
  }[];
};

@Component({
  selector: "kemp-is-reservation-step-cottages",
  imports: [FormsModule, ButtonModule],
  templateUrl: "./step-cottages.html",
  styleUrl: "./step-cottages.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StepCottages {
  readonly spots = input.required<readonly Spot[]>();
  readonly spotGroups = input.required<readonly SpotGroup[]>();
  readonly spotRows = model<readonly SpotRow[]>([]);

  // null means no constraint (e.g. reservation created at reception).
  readonly requestedByGroup = input<ReadonlyMap<string, number> | null>(null);

  protected readonly hasRequest = computed<boolean>(() => {
    const map = this.requestedByGroup();
    return map !== null && map.size > 0;
  });

  protected readonly requestSummary = computed<RequestSummary | null>(() => {
    const map = this.requestedByGroup();
    if (map === null || map.size === 0) {
      return null;
    }
    const groupById = new Map(this.spotGroups().map(g => [g.id, g]));
    const assignedByGroup = new Map<string, number>();
    for (const row of this.spotRows()) {
      if (row.spotGroupId && row.spotId !== null) {
        assignedByGroup.set(
          row.spotGroupId,
          (assignedByGroup.get(row.spotGroupId) ?? 0) + 1
        );
      }
    }

    let totalRequested = 0;
    let totalAssigned = 0;
    const groups = [...map.entries()]
      .map(([groupId, requested]) => {
        const group = groupById.get(groupId);
        const assigned = assignedByGroup.get(groupId) ?? 0;
        totalRequested += requested;
        totalAssigned += assigned;
        return { group, requested, assigned, mismatch: assigned - requested };
      })
      .filter(
        (
          entry
        ): entry is {
          group: SpotGroup;
          requested: number;
          assigned: number;
          mismatch: number;
        } => entry.group !== undefined
      )
      .sort((a, b) => a.group.name.localeCompare(b.group.name, "cs"));

    return { totalRequested, totalAssigned, groups };
  });

  protected readonly groupsView = computed<GroupView[]>(() => {
    const rows = this.spotRows();
    const rowBySpotId = new Map<string, string>();
    for (const r of rows) {
      if (r.spotId) {
        rowBySpotId.set(r.spotId, r.id);
      }
    }

    const groupsById = new Map<string, Spot[]>();
    for (const s of this.spots()) {
      if (!s.isActive) {
        continue;
      }
      const list = groupsById.get(s.spotGroupId) ?? [];
      list.push(s);
      groupsById.set(s.spotGroupId, list);
    }

    const requested = this.requestedByGroup();

    return [...this.spotGroups()]
      .filter(g => g.isActive)
      .sort((a, b) => a.name.localeCompare(b.name, "cs"))
      .map<GroupView>(g => {
        const sorted = (groupsById.get(g.id) ?? []).sort((a, b) =>
          a.name.localeCompare(b.name, "cs", { numeric: true })
        );
        const spotViews: SpotView[] = sorted.map(spot => ({
          spot,
          selected: rowBySpotId.has(spot.id),
          rowId: rowBySpotId.get(spot.id) ?? null,
        }));
        const selectedCount = spotViews.filter(s => s.selected).length;
        const requestedCount = requested?.get(g.id) ?? 0;
        return {
          group: g,
          spots: spotViews,
          selectedCount,
          availableCount: spotViews.length - selectedCount,
          requestedCount,
          mismatch: requestedCount > 0 ? selectedCount - requestedCount : 0,
        };
      });
  });

  // Sorted by spot name with numeric=true so B2 sits between B1 and B10.
  protected readonly selectedView = computed<SelectedView[]>(() => {
    const groupById = new Map(this.spotGroups().map(g => [g.id, g]));
    const spotById = new Map(this.spots().map(s => [s.id, s]));
    const views = this.spotRows().map<SelectedView>(row => ({
      row,
      group: row.spotGroupId ? (groupById.get(row.spotGroupId) ?? null) : null,
      spot: row.spotId ? (spotById.get(row.spotId) ?? null) : null,
      keyState: keyStateOf(row.hasGivenKey, row.hasReturnedKeys),
    }));
    return views.sort((a, b) => {
      if (!a.spot) {
        return b.spot ? 1 : 0;
      }
      if (!b.spot) {
        return -1;
      }
      return a.spot.name.localeCompare(b.spot.name, "cs", { numeric: true });
    });
  });

  protected readonly totalCapacity = computed<number>(() => {
    const groupById = new Map(this.spotGroups().map(g => [g.id, g]));
    return this.spotRows().reduce((sum, row) => {
      if (!row.spotGroupId) {
        return sum;
      }
      return sum + (groupById.get(row.spotGroupId)?.capacity ?? 0);
    }, 0);
  });

  protected toggleSpot(view: SpotView): void {
    if (view.selected && view.rowId) {
      this.removeSpotRow(view.rowId);
      return;
    }
    this.addSpot(view.spot);
  }

  protected removeSpotRow(id: string): void {
    this.spotRows.update(rows => rows.filter(r => r.id !== id));
  }

  private addSpot(spot: Spot): void {
    const next: SpotRow = {
      id: crypto.randomUUID() as string,
      spotGroupId: spot.spotGroupId,
      spotId: spot.id,
    };
    this.spotRows.update(rows => [...rows, next]);
  }
}
