import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";

import {
  ConfirmationService,
  type MenuItem,
  MessageService,
} from "primeng/api";
import { ButtonModule } from "primeng/button";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { MenuModule } from "primeng/menu";
import type { Popover } from "primeng/popover";
import { PopoverModule } from "primeng/popover";
import { ToastModule } from "primeng/toast";

import { EventCreateDialog } from "./event-create-dialog/event-create-dialog";
import { GroupReservationFormDialog } from "./group-reservation-form-dialog/group-reservation-form-dialog";
import { GroupReservationSummary } from "./group-reservation-summary/group-reservation-summary";
import { OutOfOrderFormDialog } from "./out-of-order-form-dialog/out-of-order-form-dialog";
import { daysInMonth, MONTHS_CZ } from "./plan-data";
import {
  type BlockClickEvent,
  type GroupBlockClickEvent,
  PlanGrid,
} from "./plan-grid";
import { ReservationSummary } from "./reservation-summary/reservation-summary";
import {
  groupReservationsToRows,
  reservationsToRows,
} from "./reservations-table/reservation-rows";
import {
  type ReservationRow,
  ReservationsTable,
} from "./reservations-table/reservations-table";
import { equal } from "../../../../utils/deepEqual";
import { ApiClient } from "../../../core/api/api-client";
import { AuthService } from "../../../core/auth/auth.service";
import { Roles } from "../../../core/auth/roles";
import { RefreshController } from "../../../core/refresh/refresh-controller";
import { dateToIso } from "../../../shared/date-iso";
import type { CalendarEvent } from "../../api/events.types";
import type { GroupReservation } from "../../api/group-reservations.types";
import type { OutOfOrder } from "../../api/out-of-orders.types";
import { PendingReservationsStore } from "../../api/pending-reservations.store";
import type { ReservationMonthlySummary } from "../../api/reservations.types";
import {
  type Reservation,
  ReservationState,
} from "../../api/reservations.types";
import type { Spot, SpotGroup, SpotStateRecord } from "../../api/spots.types";
import {
  SPOT_STATE_CONFIGS,
  SPOT_STATES,
  type SpotStateConfig,
} from "../../shared/spot-state";

type ViewMode = "plachta" | "tabulka" | "zrusene" | "skupinove" | "cekaci";

type ViewTab = {
  readonly id: ViewMode;
  readonly label: string;
  readonly icon: string;
  readonly badge?: number;
};

type MonthTab = {
  readonly idx: number;
  readonly label: string;
  readonly count: number | null;
};

type CottageStatusLegendItem = {
  readonly config: SpotStateConfig;
};

@Component({
  selector: "kemp-is-reservation-plan",
  imports: [
    FormsModule,
    ButtonModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MenuModule,
    PopoverModule,
    ToastModule,
    EventCreateDialog,
    GroupReservationFormDialog,
    GroupReservationSummary,
    OutOfOrderFormDialog,
    PlanGrid,
    ReservationSummary,
    ReservationsTable,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./reservation-plan.page.html",
  styleUrl: "./reservation-plan.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservationPlanPage {
  private readonly apiClient = inject(ApiClient);
  private readonly auth = inject(AuthService);
  private readonly pendingReservationsStore = inject(PendingReservationsStore);
  private readonly refresh = inject(RefreshController);
  private readonly messageService = inject(MessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly isManager = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Manager)
  );

  protected readonly isReceptionist = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Receptionist)
  );

  protected readonly summaryPopover = viewChild.required<Popover>("summary");
  protected readonly groupSummaryPopover =
    viewChild.required<Popover>("groupSummary");

  protected readonly view = signal<ViewMode>("plachta");
  protected readonly searchTerm = signal<string>("");
  protected readonly year = signal<number>(new Date().getFullYear());
  protected readonly monthIdx = signal<number>(new Date().getMonth());
  protected readonly selectedReservationId = signal<string | null>(null);
  protected readonly selectedGroupReservationId = signal<string | null>(null);
  protected readonly selectedGroupSpotId = signal<string | null>(null);
  protected readonly eventDialogOpen = signal<boolean>(false);
  protected readonly eventToEdit = signal<CalendarEvent | null>(null);
  protected readonly groupDialogOpen = signal<boolean>(false);
  protected readonly groupToEdit = signal<string | null>(null);
  protected readonly oooDialogOpen = signal<boolean>(false);
  protected readonly oooToEdit = signal<OutOfOrder | null>(null);

  // Calling hide() on an already-hidden popover is a no-op, so we only
  // bump the ignore counter when an actually-visible popover is hidden.
  private summaryShown = false;
  private groupSummaryShown = false;

  // When the user clicks a different block while a popover is open we
  // hide-then-reshow on the new target; the resulting onHide would
  // otherwise wipe selection and block highlight. Counter is bumped per
  // programmatic hide so the matching onHide skips cleanup.
  private summaryHidesToIgnore = 0;
  private groupSummaryHidesToIgnore = 0;

  protected readonly rowHeight = 40;

  private readonly monthRange = computed(() => {
    const y = this.year();
    const m = this.monthIdx();
    const last = daysInMonth(y, m);
    return {
      from: dateToIso(new Date(y, m, 1)),
      to: dateToIso(new Date(y, m, last)),
    };
  });

  protected readonly todayDay = computed(() => {
    const now = new Date();
    return now.getFullYear() === this.year() &&
      now.getMonth() === this.monthIdx()
      ? now.getDate()
      : 0;
  });

  protected readonly spotGroups = httpResource<readonly SpotGroup[]>(
    () => this.apiClient.url("/spot-groups"),
    { equal }
  );

  protected readonly spots = httpResource<readonly Spot[]>(
    () => this.apiClient.url("/spots"),
    { equal }
  );

  protected readonly spotStates = httpResource<readonly SpotStateRecord[]>(
    () => this.apiClient.url("/spots/states"),
    { equal }
  );

  protected readonly outOfOrders = httpResource<readonly OutOfOrder[]>(
    () => this.apiClient.url("/out-of-orders"),
    { equal }
  );

  protected readonly events = httpResource<readonly CalendarEvent[]>(
    () => this.apiClient.url("/events"),
    { equal }
  );

  protected readonly reservations = httpResource<readonly Reservation[]>(
    () => {
      const range = this.monthRange();
      const params = new URLSearchParams({ from: range.from, to: range.to });
      return `${this.apiClient.url("/reservations")}?${params.toString()}`;
    },
    { equal }
  );

  protected readonly groupReservations = httpResource<
    readonly GroupReservation[]
  >(
    () => {
      const range = this.monthRange();
      const params = new URLSearchParams({ from: range.from, to: range.to });
      return `${this.apiClient.url("/group-reservations")}?${params.toString()}`;
    },
    { equal }
  );

  protected readonly monthlySummary = httpResource<ReservationMonthlySummary>(
    () => {
      const params = new URLSearchParams({ year: String(this.year()) });
      return `${this.apiClient.url("/reservations/monthly-summary")}?${params.toString()}`;
    },
    { equal }
  );

  protected readonly views = computed<readonly ViewTab[]>(() => {
    const pendingCount = this.pendingReservationsStore.count();
    return [
      { id: "plachta", label: "Plachta", icon: "pi-table" },
      { id: "tabulka", label: "Tabulka", icon: "pi-list" },
      { id: "zrusene", label: "Zrušené rezervace", icon: "pi-times-circle" },
      { id: "skupinove", label: "Skupinové rezervace", icon: "pi-users" },
      {
        id: "cekaci",
        label: "K potvrzení",
        icon: "pi-clock",
        ...(pendingCount > 0 ? { badge: pendingCount } : {}),
      },
    ];
  });

  protected readonly months = computed<readonly MonthTab[]>(() => {
    const summary = this.monthlySummary.hasValue()
      ? this.monthlySummary.value()
      : null;
    return MONTHS_CZ.map((label, idx) => ({
      idx,
      label,
      count:
        summary && summary.year === this.year()
          ? (summary.months[idx] ?? 0)
          : null,
    }));
  });

  protected readonly statusLegend = [
    { label: "Zaplaceno", bg: "#fce7f3", accent: "#ec4899" },
    { label: "Skupinová", bg: "#fef3c7", accent: "#f59e0b" },
    { label: "Ze skupiny", bg: "#ccfbf1", accent: "#0d9488" },
    { label: "Potvrzená", bg: "#eef2ff", accent: "#6366f1" },
  ] as const;

  protected readonly cottageStatusLegend: readonly CottageStatusLegendItem[] =
    SPOT_STATES.map(id => ({ config: SPOT_STATE_CONFIGS[id] }));

  protected readonly isLoading = computed(
    () =>
      this.spotGroups.isLoading() ||
      this.spots.isLoading() ||
      this.spotStates.isLoading() ||
      this.outOfOrders.isLoading() ||
      this.events.isLoading() ||
      this.reservations.isLoading() ||
      this.groupReservations.isLoading()
  );

  protected readonly loadError = computed<string | null>(() => {
    const sources = [
      this.spotGroups,
      this.spots,
      this.spotStates,
      this.outOfOrders,
      this.events,
      this.reservations,
      this.pendingReservationsStore.resource,
      this.groupReservations,
    ] as const;
    for (const src of sources) {
      if (src.error()) {
        return "Nepodařilo se načíst data ubytovacího plánu.";
      }
    }
    return null;
  });

  // Default to empty arrays before the first httpResource response lands.
  protected readonly spotGroupsValue = computed<readonly SpotGroup[]>(() =>
    this.spotGroups.hasValue() ? this.spotGroups.value() : []
  );
  protected readonly spotsValue = computed<readonly Spot[]>(() =>
    this.spots.hasValue() ? this.spots.value() : []
  );
  protected readonly spotStatesValue = computed<readonly SpotStateRecord[]>(
    () => (this.spotStates.hasValue() ? this.spotStates.value() : [])
  );
  protected readonly reservationsValue = computed<readonly Reservation[]>(() =>
    this.reservations.hasValue() ? this.reservations.value() : []
  );
  protected readonly outOfOrdersValue = computed<readonly OutOfOrder[]>(() =>
    this.outOfOrders.hasValue() ? this.outOfOrders.value() : []
  );
  protected readonly eventsValue = computed<readonly CalendarEvent[]>(() =>
    this.events.hasValue() ? this.events.value() : []
  );
  protected readonly groupReservationsValue = computed<
    readonly GroupReservation[]
  >(() =>
    this.groupReservations.hasValue() ? this.groupReservations.value() : []
  );

  private readonly normalizedSearch = computed<string>(() =>
    this.searchTerm().trim().toLocaleLowerCase("cs")
  );

  protected readonly filteredReservations = computed<readonly Reservation[]>(
    () => {
      const q = this.normalizedSearch();
      if (q.length === 0) {
        return this.reservationsValue();
      }
      return this.reservationsValue().filter(r =>
        matchesFields(q, [
          r.number,
          r.reservationMakerName,
          r.reservationMakerSurname,
          r.reservationMakerPhone,
          r.displayName ?? "",
        ])
      );
    }
  );

  protected readonly filteredGroupReservations = computed<
    readonly GroupReservation[]
  >(() => {
    const q = this.normalizedSearch();
    if (q.length === 0) {
      return this.groupReservationsValue();
    }
    return this.groupReservationsValue().filter(g =>
      matchesFields(q, [
        g.id.slice(0, 8),
        g.organizerName,
        g.organizerPhone,
        g.displayName ?? "",
      ])
    );
  });

  private readonly filteredPendingReservations = computed<
    readonly Reservation[]
  >(() => {
    const q = this.normalizedSearch();
    const source = this.pendingReservationsStore.value();
    if (q.length === 0) {
      return source;
    }
    return source.filter(r =>
      matchesFields(q, [
        r.number,
        r.reservationMakerName,
        r.reservationMakerSurname,
        r.reservationMakerPhone,
        r.displayName ?? "",
      ])
    );
  });

  private readonly allReservationRows = computed<readonly ReservationRow[]>(
    () => reservationsToRows(this.filteredReservations(), this.spotsValue())
  );

  protected readonly activeReservationRows = computed<
    readonly ReservationRow[]
  >(() => {
    const visibleStates: readonly ReservationState[] = [
      ReservationState.Confirmed,
      ReservationState.CheckedIn,
      ReservationState.Completed,
    ];
    const filtered = this.filteredReservations().filter(r =>
      visibleStates.includes(r.state)
    );
    return reservationsToRows(filtered, this.spotsValue());
  });

  protected readonly cancelledReservationRows = computed<
    readonly ReservationRow[]
  >(() => this.allReservationRows().filter(r => r.stateKind === "cancelled"));

  protected readonly groupReservationRows = computed<readonly ReservationRow[]>(
    () => groupReservationsToRows(this.filteredGroupReservations())
  );

  protected readonly pendingReservationRows = computed<
    readonly ReservationRow[]
  >(() =>
    reservationsToRows(this.filteredPendingReservations(), this.spotsValue())
  );

  protected readonly tableLoading = computed(
    () =>
      this.reservations.isLoading() ||
      this.pendingReservationsStore.resource.isLoading() ||
      this.spots.isLoading() ||
      this.spotGroups.isLoading() ||
      this.groupReservations.isLoading()
  );

  constructor() {
    effect(() => {
      const t = this.refresh.tick();
      if (t === 0) {
        return;
      }
      untracked(() => {
        this.spotGroups.reload();
        this.spots.reload();
        this.spotStates.reload();
        this.outOfOrders.reload();
        this.events.reload();
        this.reservations.reload();
        this.groupReservations.reload();
        this.monthlySummary.reload();
      });
    });

    // Dashboard "New" menu deep-links here with ?create=<kind> for inline
    // dialog flows. Strip the param so a refresh doesn't reopen the dialog.
    const kind = this.route.snapshot.queryParamMap.get("create");
    if (
      kind === "group-reservation" ||
      kind === "event" ||
      kind === "out-of-order"
    ) {
      queueMicrotask(() => {
        if (kind === "event") {
          this.eventToEdit.set(null);
          this.eventDialogOpen.set(true);
        } else if (kind === "group-reservation") {
          this.groupToEdit.set(null);
          this.groupDialogOpen.set(true);
        } else {
          this.oooToEdit.set(null);
          this.oooDialogOpen.set(true);
        }
        void this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { create: null },
          queryParamsHandling: "merge",
          replaceUrl: true,
        });
      });
    }
  }

  protected onBlockClick(event: BlockClickEvent): void {
    // Two popovers (per-reservation and per-group); keep at most one
    // open. Hide-then-reshow this popover so clicking a different block
    // of the same kind re-anchors - PrimeNG's Popover.show() skips the
    // re-position when the overlay is already visible. setTimeout lets
    // Angular tear down the previous overlay before opening a fresh one.
    // Only the same-kind hide is ignored; cross-kind hides run cleanup.
    if (this.groupSummaryShown) {
      this.groupSummaryPopover().hide();
    }
    if (this.summaryShown) {
      this.summaryHidesToIgnore++;
      this.summaryPopover().hide();
    }
    this.selectedReservationId.set(event.reservationId);
    setTimeout(() =>
      this.summaryPopover().show(event.originalEvent, event.target)
    );
  }

  protected onRowClicked(reservationId: string): void {
    void this.router.navigate([
      "/staff/auth/desktop/reservations",
      reservationId,
      "edit",
    ]);
  }

  protected onGroupRowClicked(groupReservationId: string): void {
    this.groupToEdit.set(groupReservationId);
    this.groupDialogOpen.set(true);
  }

  protected onGroupBlockClick(event: GroupBlockClickEvent): void {
    if (this.summaryShown) {
      this.summaryPopover().hide();
    }
    if (this.groupSummaryShown) {
      this.groupSummaryHidesToIgnore++;
      this.groupSummaryPopover().hide();
    }
    this.selectedGroupReservationId.set(event.groupReservationId);
    this.selectedGroupSpotId.set(event.spotId);
    setTimeout(() =>
      this.groupSummaryPopover().show(event.originalEvent, event.target)
    );
  }

  protected onGroupSummaryShow(): void {
    this.groupSummaryShown = true;
  }

  protected onGroupSummaryHide(): void {
    this.groupSummaryShown = false;
    if (this.groupSummaryHidesToIgnore > 0) {
      this.groupSummaryHidesToIgnore--;
      return;
    }
    this.selectedGroupReservationId.set(null);
    this.selectedGroupSpotId.set(null);
  }

  protected onEditGroupFromSummary(groupReservationId: string): void {
    this.groupSummaryPopover().hide();
    this.groupToEdit.set(groupReservationId);
    this.groupDialogOpen.set(true);
  }

  protected onCreateReservationForGroup(payload: {
    readonly groupReservationId: string;
    readonly spotId: string;
  }): void {
    this.groupSummaryPopover().hide();
    void this.router.navigate(["/staff/auth/desktop/reservations/new"], {
      queryParams: {
        groupReservationId: payload.groupReservationId,
        spot: payload.spotId,
      },
    });
  }

  protected onGroupReservationMutated(
    kind: "created" | "updated" | "canceled"
  ): void {
    this.groupReservations.reload();
    const detail =
      kind === "created"
        ? "Skupinová rezervace vytvořena"
        : kind === "updated"
          ? "Skupinová rezervace uložena"
          : "Skupinová rezervace zrušena";
    this.messageService.add({
      severity: "success",
      summary: "Hotovo",
      detail,
    });
  }

  protected onOOOBandClick(oooId: string): void {
    const target = this.outOfOrdersValue().find(o => o.id === oooId);
    if (!target) {
      return;
    }
    this.oooToEdit.set(target);
    this.oooDialogOpen.set(true);
  }

  protected onOOOMutated(kind: "created" | "updated" | "deleted"): void {
    this.outOfOrders.reload();
    const detail =
      kind === "created"
        ? "Záznam mimo provoz vytvořen"
        : kind === "updated"
          ? "Záznam mimo provoz uložen"
          : "Záznam mimo provoz smazán";
    this.messageService.add({
      severity: "success",
      summary: "Hotovo",
      detail,
    });
  }

  protected onSummaryShow(): void {
    this.summaryShown = true;
  }

  protected onSummaryHide(): void {
    this.summaryShown = false;
    if (this.summaryHidesToIgnore > 0) {
      this.summaryHidesToIgnore--;
      return;
    }
    this.selectedReservationId.set(null);
  }

  protected onEditReservation(): void {
    const id = this.selectedReservationId();
    this.summaryPopover().hide();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/reservations", id, "edit"]);
  }

  protected readonly createMenuItems = computed<MenuItem[]>(() => {
    const items: MenuItem[] = [
      {
        label: "Rezervace",
        icon: "pi pi-calendar-plus",
        command: () => this.onCreate("reservation"),
      },
      {
        label: "Skupinová rezervace",
        icon: "pi pi-users",
        command: () => this.onCreate("group-reservation"),
      },
    ];
    if (this.isReceptionist()) {
      items.push({
        label: "Účtenka",
        icon: "pi pi-receipt",
        command: () => this.onCreate("bill"),
      });
    }
    if (this.isManager()) {
      items.push({
        label: "Akce",
        icon: "pi pi-megaphone",
        command: () => this.onCreate("event"),
      });
    }
    items.push({
      label: "Mimo provoz",
      icon: "pi pi-wrench",
      command: () => this.onCreate("out-of-order"),
    });
    return items;
  });

  protected onCreate(
    kind:
      | "reservation"
      | "group-reservation"
      | "event"
      | "out-of-order"
      | "bill"
  ): void {
    if (kind === "reservation") {
      void this.router.navigate(["/staff/auth/desktop/reservations/new"]);
      return;
    }
    if (kind === "bill") {
      void this.router.navigate(["/staff/auth/desktop/bill/new"]);
      return;
    }
    if (kind === "event") {
      this.eventToEdit.set(null);
      this.eventDialogOpen.set(true);
      return;
    }
    if (kind === "group-reservation") {
      this.groupToEdit.set(null);
      this.groupDialogOpen.set(true);
      return;
    }
    this.oooToEdit.set(null);
    this.oooDialogOpen.set(true);
  }

  protected onEventBandClick(eventId: string): void {
    const target = this.eventsValue().find(e => e.id === eventId);
    if (!target) {
      return;
    }
    this.eventToEdit.set(target);
    this.eventDialogOpen.set(true);
  }

  protected onEventMutated(): void {
    this.events.reload();
  }

  protected setView(view: ViewMode): void {
    this.view.set(view);
  }

  protected setMonth(idx: number): void {
    this.monthIdx.set(idx);
  }

  protected prevYear(): void {
    this.year.update(y => y - 1);
  }

  protected nextYear(): void {
    this.year.update(y => y + 1);
  }
}

function matchesFields(query: string, fields: readonly string[]): boolean {
  for (const f of fields) {
    if (f.length > 0 && f.toLocaleLowerCase("cs").includes(query)) {
      return true;
    }
  }
  return false;
}
