import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from "@angular/core";

import {
  daysInMonth,
  DOW_CZ,
  dowMon,
  KIND_STYLES,
  type ReservationKind,
} from "./plan-data";
import { isoToDate } from "../../../shared/date-iso";
import type { CalendarEvent } from "../../api/events.types";
import {
  type GroupReservation,
  GroupReservationState,
} from "../../api/group-reservations.types";
import type { OutOfOrder } from "../../api/out-of-orders.types";
import type { Reservation } from "../../api/reservations.types";
import { ReservationState } from "../../api/reservations.types";
import type { Spot, SpotGroup, SpotStateRecord } from "../../api/spots.types";
import { SpotState as ApiSpotState } from "../../api/spots.types";
import {
  mapApiSpotStateToUi,
  SPOT_STATE_CONFIGS,
  type SpotState,
  type SpotStateConfig,
} from "../../shared/spot-state";

type RowCottage = {
  readonly id: string;
  readonly name: string;
  readonly groupId: string;
  readonly groupEnd: boolean;
  readonly status: SpotState;
  readonly statusConfig: SpotStateConfig;
  // Absolute Y inside the canvas, factoring in event strips.
  readonly top: number;
};

type GroupBand = {
  readonly id: string;
  readonly name: string;
  readonly top: number;
  readonly height: number;
  // Vertical space reserved at the top for event ribbons.
  readonly eventStripHeight: number;
};

type EventBand = {
  readonly id: string;
  readonly eventId: string;
  readonly name: string;
  readonly tooltip: string;
  readonly left: number;
  readonly width: number;
  readonly top: number;
  readonly height: number;
};

const EVENT_LANE_HEIGHT = 20;

type DayCell = {
  readonly day: number;
  readonly dow: number;
  readonly dowLabel: string;
  readonly weekend: boolean;
  readonly today: boolean;
};

type ReservationBlock = {
  readonly id: string;
  readonly reservationId: string;
  // Set when the block represents a parent GroupReservation without a
  // pinned member Reservation; clicking opens the group summary popover.
  readonly groupReservationId: string | null;
  readonly spotId: string;
  readonly guest: string;
  readonly phone: string;
  readonly kind: ReservationKind;
  readonly isGroup: boolean;
  readonly left: number;
  readonly width: number;
  readonly bg: string;
  readonly border: string;
  readonly accent: string;
  readonly text: string;
  readonly phoneColor: string;
  readonly clipPath: string;
  readonly padding: string;
  readonly showPhone: boolean;
  readonly contextOpen: boolean;
  // Confirmed state and the spot reports hasGivenKey === true, i.e.
  // reception handed the key before the formal check-in transition.
  readonly hasGivenKey: boolean;
};

export type BlockClickEvent = {
  readonly reservationId: string;
  readonly target: HTMLElement;
  readonly originalEvent: MouseEvent;
};

export type GroupBlockClickEvent = {
  readonly groupReservationId: string;
  readonly spotId: string;
  readonly target: HTMLElement;
  readonly originalEvent: MouseEvent;
};

type OOOBand = {
  readonly id: string;
  readonly oooId: string;
  readonly left: number;
  readonly width: number;
  readonly fullMonth: boolean;
  readonly reason: string;
};

const ISO_DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;

function parseIsoDate(iso: string): Date | null {
  const m = ISO_DATE_RE.exec(iso);
  if (!m) {
    return null;
  }
  const [, y, mo, d] = m;
  return new Date(Number(y), Number(mo) - 1, Number(d));
}

function mapReservationKind(r: Reservation): ReservationKind {
  if (r.state === ReservationState.CheckedIn) {
    return "paid";
  }
  if (r.groupReservationId) {
    return "linkedToGroup";
  }
  return "confirmed";
}

const COMPARE_LOCALE = "cs";

function compareByName<T extends { readonly name: string }>(
  a: T,
  b: T
): number {
  return a.name.localeCompare(b.name, COMPARE_LOCALE, { numeric: true });
}

@Component({
  selector: "kemp-is-plan-grid",
  templateUrl: "./plan-grid.html",
  styleUrl: "./plan-grid.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlanGrid {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);

  readonly year = input.required<number>();
  readonly monthIdx = input.required<number>();
  readonly spotGroups = input.required<readonly SpotGroup[]>();
  readonly spots = input.required<readonly Spot[]>();
  readonly spotStates = input.required<readonly SpotStateRecord[]>();
  readonly reservations = input.required<readonly Reservation[]>();
  readonly groupReservations = input.required<readonly GroupReservation[]>();
  readonly outOfOrders = input.required<readonly OutOfOrder[]>();
  readonly events = input.required<readonly CalendarEvent[]>();
  readonly rowHeight = input<number>(40);
  readonly todayDay = input<number>(0);
  readonly contextOpenId = input<string | null>(null);
  readonly minDayWidth = input<number>(28);

  readonly blockClick = output<BlockClickEvent>();
  readonly eventClick = output<string>();
  readonly oooClick = output<string>();
  readonly groupReservationClick = output<GroupBlockClickEvent>();

  private readonly availableWidth = signal<number>(0);

  protected readonly labelColW = 56;
  protected readonly numColW = 48;
  protected readonly stateColW = 40;

  protected readonly leftHeaderW =
    this.labelColW + this.numColW + this.stateColW;

  constructor() {
    afterNextRender(() => {
      const el = this.host.nativeElement;
      this.availableWidth.set(el.clientWidth);
      const ro = new ResizeObserver(entries => {
        const entry = entries[0];
        if (entry) {
          this.availableWidth.set(Math.floor(entry.contentRect.width));
        }
      });
      ro.observe(el);
      this.destroyRef.onDestroy(() => ro.disconnect());
    });
  }

  protected readonly dayWidth = computed(() => {
    const w = this.availableWidth();
    const total = this.days().length;
    const min = this.minDayWidth();
    if (w === 0 || total === 0) {
      return min;
    }
    const usable = w - this.leftHeaderW;
    const candidate = Math.floor(usable / total);
    return Math.max(min, candidate);
  });

  protected readonly days = computed<readonly DayCell[]>(() => {
    const y = this.year();
    const m = this.monthIdx();
    const total = daysInMonth(y, m);
    const today = this.todayDay();
    const cells: DayCell[] = [];
    for (let i = 0; i < total; i++) {
      const day = i + 1;
      const dow = dowMon(y, m, day);
      cells.push({
        day,
        dow,
        dowLabel: DOW_CZ[dow] ?? "",
        weekend: dow >= 5,
        today: day === today,
      });
    }
    return cells;
  });

  private readonly sortedGroups = computed<readonly SpotGroup[]>(() =>
    [...this.spotGroups()].sort(compareByName)
  );

  private readonly spotsByGroupSorted = computed<
    ReadonlyMap<string, readonly Spot[]>
  >(() => {
    const map = new Map<string, Spot[]>();
    for (const s of this.spots()) {
      if (!s.isActive) {
        continue;
      }
      const list = map.get(s.spotGroupId) ?? [];
      list.push(s);
      map.set(s.spotGroupId, list);
    }
    const out = new Map<string, readonly Spot[]>();
    for (const [k, list] of map) {
      out.set(k, [...list].sort(compareByName));
    }
    return out;
  });

  protected readonly groupBands = computed<readonly GroupBand[]>(() => {
    const rh = this.rowHeight();
    const lanesByGroup = this.eventLanesByGroup();
    const bands: GroupBand[] = [];
    let top = 0;
    for (const g of this.sortedGroups()) {
      const list = this.spotsByGroupSorted().get(g.id);
      const count = list ? list.length : 0;
      if (count === 0) {
        continue;
      }
      const laneCount = lanesByGroup.get(g.id)?.length ?? 0;
      const eventStripHeight = laneCount * EVENT_LANE_HEIGHT;
      const height = eventStripHeight + count * rh;
      bands.push({ id: g.id, name: g.name, top, height, eventStripHeight });
      top += height;
    }
    return bands;
  });

  // Spots affected by an OOO active right now get "ooo" status today.
  private readonly oooActiveSpotIds = computed<ReadonlySet<string>>(() => {
    const now = Date.now();
    const set = new Set<string>();
    for (const ooo of this.outOfOrders()) {
      const from = parseLocalDate(ooo.from).getTime();
      const to = parseLocalDate(ooo.to).getTime();
      if (Number.isNaN(from) || Number.isNaN(to)) {
        continue;
      }
      if (from > now || to < now) {
        continue;
      }
      for (const id of ooo.spotIds) {
        set.add(id);
      }
      if (ooo.spotGroupIds.length > 0) {
        for (const s of this.spots()) {
          if (ooo.spotGroupIds.includes(s.spotGroupId)) {
            set.add(s.id);
          }
        }
      }
    }
    return set;
  });

  private readonly stateBySpot = computed<ReadonlyMap<string, ApiSpotState>>(
    () => {
      const m = new Map<string, ApiSpotState>();
      for (const s of this.spotStates()) {
        m.set(s.spotId, s.state);
      }
      return m;
    }
  );

  private readonly hasGivenKeyBySpot = computed<ReadonlyMap<string, boolean>>(
    () => {
      const m = new Map<string, boolean>();
      for (const s of this.spotStates()) {
        m.set(s.spotId, s.hasGivenKey);
      }
      return m;
    }
  );

  protected readonly cottages = computed<readonly RowCottage[]>(() => {
    const oooNow = this.oooActiveSpotIds();
    const stateBy = this.stateBySpot();
    const rh = this.rowHeight();
    const lanesByGroup = this.eventLanesByGroup();
    const rows: RowCottage[] = [];
    let runningTop = 0;
    for (const g of this.sortedGroups()) {
      const list = this.spotsByGroupSorted().get(g.id);
      if (!list || list.length === 0) {
        continue;
      }
      const laneCount = lanesByGroup.get(g.id)?.length ?? 0;
      runningTop += laneCount * EVENT_LANE_HEIGHT;
      const last = list.length - 1;
      list.forEach((spot, idx) => {
        const status: SpotState = oooNow.has(spot.id)
          ? "ooo"
          : mapApiSpotStateToUi(
              stateBy.get(spot.id) ?? ApiSpotState.Unoccupied
            );
        rows.push({
          id: spot.id,
          name: spot.name,
          groupId: g.id,
          groupEnd: idx === last,
          status,
          statusConfig: SPOT_STATE_CONFIGS[status],
          top: runningTop + idx * rh,
        });
      });
      runningTop += list.length * rh;
    }
    return rows;
  });

  protected readonly todayLineLeft = computed(
    () => this.leftHeaderW + (this.todayDay() - 1) * this.dayWidth()
  );

  protected readonly todayLineVisible = computed(() => this.todayDay() > 0);

  protected readonly totalGridWidth = computed(
    () => this.leftHeaderW + this.days().length * this.dayWidth()
  );

  protected readonly totalGridHeight = computed(() => {
    const bands = this.groupBands();
    if (bands.length === 0) {
      return 0;
    }
    const last = bands[bands.length - 1];
    if (!last) {
      return 0;
    }
    return last.top + last.height;
  });

  private readonly visibleMonthBounds = computed(() => {
    const y = this.year();
    const m = this.monthIdx();
    const last = daysInMonth(y, m);
    return {
      start: new Date(y, m, 1),
      endExclusive: new Date(y, m + 1, 1),
      lastDay: last,
    };
  });

  private readonly cottagesById = computed<ReadonlyMap<string, RowCottage>>(
    () => {
      const m = new Map<string, RowCottage>();
      for (const r of this.cottages()) {
        m.set(r.id, r);
      }
      return m;
    }
  );

  private readonly blocksByRow = computed<
    ReadonlyMap<string, readonly ReservationBlock[]>
  >(() => {
    const dw = this.dayWidth();
    const ctxId = this.contextOpenId();
    const { start, endExclusive, lastDay } = this.visibleMonthBounds();
    const cottagesById = this.cottagesById();
    const hasGivenKeyBySpot = this.hasGivenKeyBySpot();
    const out = new Map<string, ReservationBlock[]>();

    for (const r of this.reservations()) {
      if (
        r.state !== ReservationState.Confirmed &&
        r.state !== ReservationState.CheckedIn
      ) {
        continue;
      }
      const fromDate = parseIsoDate(r.from);
      const toDate = parseIsoDate(r.to);
      if (!fromDate || !toDate) {
        continue;
      }

      const leftClamped = fromDate < start;
      const rightClamped = toDate >= endExclusive;
      const fromDay = leftClamped ? 1 : fromDate.getDate();
      const toDay = rightClamped ? lastDay + 1 : toDate.getDate();
      if (fromDay >= toDay) {
        continue;
      }

      const kind = mapReservationKind(r);
      const style = KIND_STYLES[kind];
      const half = dw / 2;
      const startX = leftClamped ? 0 : (fromDay - 1) * dw + half;
      const endX = rightClamped ? lastDay * dw : (toDay - 1) * dw + half;
      const left = startX;
      const width = Math.max(0, endX - startX - 2);
      const leftCut = leftClamped ? 0 : half;
      const rightCut = rightClamped ? 0 : half;
      const clipPath = `polygon(${leftCut}px 0, 100% 0, calc(100% - ${rightCut}px) 100%, 0 100%)`;
      const padLeft = leftClamped ? 8 : half + 8;
      const padRight = rightClamped ? 6 : half + 6;
      const padding = `4px ${padRight}px 4px ${padLeft}px`;
      const customLabel = r.displayName?.trim() ?? "";
      const guest =
        customLabel ||
        r.reservationMakerSurname.trim() ||
        r.reservationMakerName;

      for (const spotId of r.spotItems) {
        if (!cottagesById.has(spotId)) {
          continue;
        }
        const list = out.get(spotId) ?? [];
        const hasGivenKey =
          r.state === ReservationState.Confirmed &&
          (hasGivenKeyBySpot.get(spotId) ?? false);
        list.push({
          id: `${r.id}:${spotId}`,
          reservationId: r.id,
          groupReservationId: null,
          spotId,
          guest,
          phone: r.reservationMakerPhone,
          kind,
          isGroup: kind === "group",
          left,
          width,
          bg: style.bg,
          border: style.border,
          accent: style.accent,
          text: style.text,
          phoneColor: style.phone,
          clipPath,
          padding,
          showPhone: width > 110,
          contextOpen: ctxId === r.id,
          hasGivenKey,
        });
        out.set(spotId, list);
      }
    }

    // Skip group bands on spots already covered by a member reservation
    // above to avoid duplicate bands once members are registered.
    const groupKind: ReservationKind = "group";
    const groupStyle = KIND_STYLES[groupKind];
    for (const gr of this.groupReservations()) {
      if (gr.state !== GroupReservationState.Confirmed) {
        continue;
      }
      const fromDate = parseIsoDate(gr.from);
      const toDate = parseIsoDate(gr.to);
      if (!fromDate || !toDate) {
        continue;
      }

      const leftClamped = fromDate < start;
      const rightClamped = toDate >= endExclusive;
      const fromDay = leftClamped ? 1 : fromDate.getDate();
      const toDay = rightClamped ? lastDay + 1 : toDate.getDate();
      if (fromDay >= toDay) {
        continue;
      }

      const half = dw / 2;
      const startX = leftClamped ? 0 : (fromDay - 1) * dw + half;
      const endX = rightClamped ? lastDay * dw : (toDay - 1) * dw + half;
      const left = startX;
      const width = Math.max(0, endX - startX - 2);
      const leftCut = leftClamped ? 0 : half;
      const rightCut = rightClamped ? 0 : half;
      const clipPath = `polygon(${leftCut}px 0, 100% 0, calc(100% - ${rightCut}px) 100%, 0 100%)`;
      const padLeft = leftClamped ? 8 : half + 8;
      const padRight = rightClamped ? 6 : half + 6;
      const padding = `4px ${padRight}px 4px ${padLeft}px`;

      const groupCustomLabel = gr.displayName?.trim() ?? "";
      const groupGuest = groupCustomLabel || gr.organizerName;
      for (const spotId of gr.spotIds) {
        if (!cottagesById.has(spotId)) {
          continue;
        }
        const existing = out.get(spotId) ?? [];
        const hasMemberCovering = existing.some(
          b =>
            !b.groupReservationId &&
            b.left < left + width &&
            b.left + b.width > left
        );
        if (hasMemberCovering) {
          continue;
        }
        const list = existing;
        list.push({
          id: `group:${gr.id}:${spotId}`,
          reservationId: "",
          groupReservationId: gr.id,
          spotId,
          guest: groupGuest,
          phone: gr.organizerPhone,
          kind: groupKind,
          isGroup: true,
          left,
          width,
          bg: groupStyle.bg,
          border: groupStyle.border,
          accent: groupStyle.accent,
          text: groupStyle.text,
          phoneColor: groupStyle.phone,
          clipPath,
          padding,
          showPhone: width > 110,
          contextOpen: false,
          hasGivenKey: false,
        });
        out.set(spotId, list);
      }
    }
    return out;
  });

  private readonly oooBandsByRow = computed<
    ReadonlyMap<string, readonly OOOBand[]>
  >(() => {
    const dw = this.dayWidth();
    const { start, endExclusive, lastDay } = this.visibleMonthBounds();
    const cottagesById = this.cottagesById();
    const out = new Map<string, OOOBand[]>();

    for (const ooo of this.outOfOrders()) {
      const fromDate = parseLocalDate(ooo.from);
      const toDate = parseLocalDate(ooo.to);
      if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime())) {
        continue;
      }
      if (fromDate >= endExclusive || toDate < start) {
        continue;
      }

      const fromDay = fromDate < start ? 1 : fromDate.getDate();
      const toDay =
        toDate >= endExclusive
          ? lastDay + 1
          : Math.min(lastDay + 1, toDate.getDate() + 1);
      if (fromDay >= toDay) {
        continue;
      }

      const left = (fromDay - 1) * dw;
      const width = (toDay - fromDay) * dw;
      const fullMonth = fromDay === 1 && toDay === lastDay + 1;

      const affected = new Set<string>(ooo.spotIds);
      if (ooo.spotGroupIds.length > 0) {
        for (const s of this.spots()) {
          if (ooo.spotGroupIds.includes(s.spotGroupId)) {
            affected.add(s.id);
          }
        }
      }
      for (const spotId of affected) {
        if (!cottagesById.has(spotId)) {
          continue;
        }
        const list = out.get(spotId) ?? [];
        list.push({
          id: `${ooo.id}:${spotId}`,
          oooId: ooo.id,
          left,
          width,
          fullMonth,
          reason: ooo.reason,
        });
        out.set(spotId, list);
      }
    }
    return out;
  });

  // Lanes computed in date-space (not dayWidth) so the lane count (which
  // drives group height) does not change on window resize. Event bounds
  // are DateOnly strings treated as inclusive intervals.
  private readonly eventLanesByGroup = computed<
    ReadonlyMap<string, readonly (readonly CalendarEvent[])[]>
  >(() => {
    const { start, endExclusive } = this.visibleMonthBounds();
    const startMs = start.getTime();
    const endExclusiveMs = endExclusive.getTime();
    const dayMs = 86_400_000;

    const visible = [...this.events()]
      .filter(e => {
        const s = isoToDate(e.startsAt);
        const end = isoToDate(e.endsAt);
        if (!s || !end) {
          return false;
        }
        return end.getTime() + dayMs > startMs && s.getTime() < endExclusiveMs;
      })
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt));

    const out = new Map<string, CalendarEvent[][]>();
    for (const event of visible) {
      const sStart = isoToDate(event.startsAt);
      const sEnd = isoToDate(event.endsAt);
      if (!sStart || !sEnd) {
        continue;
      }
      const sStartMs = sStart.getTime();
      const sEndMs = sEnd.getTime() + dayMs;
      for (const groupId of event.spotGroupIds) {
        const lanes = out.get(groupId) ?? [];
        let laneIdx = 0;
        for (;;) {
          const lane = lanes[laneIdx];
          if (
            !lane ||
            !lane.some(le => {
              const leStart = isoToDate(le.startsAt);
              const leEnd = isoToDate(le.endsAt);
              if (!leStart || !leEnd) {
                return false;
              }
              return (
                sStartMs < leEnd.getTime() + dayMs && sEndMs > leStart.getTime()
              );
            })
          ) {
            break;
          }
          laneIdx++;
        }
        while (lanes.length <= laneIdx) {
          lanes.push([]);
        }
        const placed = lanes[laneIdx];
        if (placed) {
          placed.push(event);
        }
        out.set(groupId, lanes);
      }
    }
    return out;
  });

  protected readonly eventBands = computed<readonly EventBand[]>(() => {
    const dw = this.dayWidth();
    const { start, endExclusive, lastDay } = this.visibleMonthBounds();
    const lh = this.leftHeaderW;
    const lanesByGroup = this.eventLanesByGroup();
    const groupTopById = new Map<string, number>();
    for (const band of this.groupBands()) {
      groupTopById.set(band.id, band.top);
    }

    const out: EventBand[] = [];
    for (const [groupId, lanes] of lanesByGroup) {
      const groupTop = groupTopById.get(groupId);
      if (groupTop === undefined) {
        continue;
      }
      lanes.forEach((lane, laneIdx) => {
        for (const event of lane) {
          const sDate = isoToDate(event.startsAt);
          const eDate = isoToDate(event.endsAt);
          if (!sDate || !eDate) {
            continue;
          }
          const fromDay = sDate < start ? 1 : sDate.getDate();
          // Inclusive end: ribbon spans through the end of that day.
          const toDay =
            eDate >= endExclusive
              ? lastDay + 1
              : Math.min(lastDay + 1, eDate.getDate() + 1);
          if (fromDay >= toDay) {
            continue;
          }
          const left = lh + (fromDay - 1) * dw;
          const width = Math.max(0, (toDay - fromDay) * dw - 2);
          const tooltip = event.description
            ? `${event.name} — ${event.description}`
            : event.name;
          out.push({
            id: `${event.id}:${groupId}`,
            eventId: event.id,
            name: event.name,
            tooltip,
            left,
            width,
            top: groupTop + laneIdx * EVENT_LANE_HEIGHT,
            height: EVENT_LANE_HEIGHT - 2,
          });
        }
      });
    }
    return out;
  });

  protected blocksFor(row: RowCottage): readonly ReservationBlock[] {
    return this.blocksByRow().get(row.id) ?? [];
  }

  protected oooFor(row: RowCottage): readonly OOOBand[] {
    return this.oooBandsByRow().get(row.id) ?? [];
  }

  protected onEventBandClick(eventId: string, event: MouseEvent): void {
    event.stopPropagation();
    this.eventClick.emit(eventId);
  }

  protected onOOOBandClick(band: OOOBand, event: MouseEvent): void {
    event.stopPropagation();
    this.oooClick.emit(band.oooId);
  }

  protected onBlockClick(block: ReservationBlock, event: MouseEvent): void {
    event.stopPropagation();
    const target = event.currentTarget;
    if (!(target instanceof HTMLElement)) {
      return;
    }
    if (block.groupReservationId) {
      this.groupReservationClick.emit({
        groupReservationId: block.groupReservationId,
        spotId: block.spotId,
        target,
        originalEvent: event,
      });
      return;
    }
    this.blockClick.emit({
      reservationId: block.reservationId,
      target,
      originalEvent: event,
    });
  }
}

function parseLocalDate(s: string): Date {
  const [y, m, d] = s.split("-").map(Number) as [number, number, number];
  return new Date(y, m - 1, d);
}
