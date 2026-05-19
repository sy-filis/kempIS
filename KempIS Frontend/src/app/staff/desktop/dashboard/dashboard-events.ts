import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { TagModule } from "primeng/tag";

import { ApiClient } from "../../../core/api/api-client";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import type { CalendarEvent } from "../../api/events.types";

type EventStatus = "past" | "upcoming";

type DashEventRow = {
  readonly id: string;
  readonly title: string;
  readonly startTime?: string;
  readonly day?: string;
  readonly status: EventStatus;
};

const LOCALE = "cs-CZ";
const MS_PER_DAY = 1000 * 60 * 60 * 24;

@Component({
  selector: "kemp-is-dash-events",
  imports: [TagModule],
  templateUrl: "./dashboard-events.html",
  styleUrl: "./dashboard-events.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashEventsPanel {
  private readonly apiClient = inject(ApiClient);

  private readonly events = httpResource<readonly CalendarEvent[]>(() =>
    this.apiClient.url("/events")
  );

  private readonly todayIso = dateToIso(new Date());

  protected readonly today = computed<readonly DashEventRow[]>(() => {
    if (!this.events.hasValue()) {
      return [];
    }
    return this.events
      .value()
      .filter(e => this.coversDay(e, this.todayIso))
      .map(e => ({
        id: e.id,
        title: e.name,
        startTime: this.formatStart(e.startsAt),
        status: this.eventStatus(e),
      }));
  });

  protected readonly upcoming = computed<readonly DashEventRow[]>(() => {
    if (!this.events.hasValue()) {
      return [];
    }
    const today = isoToDate(this.todayIso);
    if (!today) {
      return [];
    }
    const horizon = new Date(today.getTime() + 7 * MS_PER_DAY);
    return this.events
      .value()
      .filter(e => {
        const start = isoToDate(e.startsAt);
        return (
          start !== null &&
          start.getTime() > today.getTime() &&
          start.getTime() <= horizon.getTime()
        );
      })
      .sort((a, b) => a.startsAt.localeCompare(b.startsAt))
      .map(e => ({
        id: e.id,
        title: e.name,
        day: this.formatShortDay(e.startsAt),
        status: "upcoming" as const,
      }));
  });

  private coversDay(e: CalendarEvent, dayIso: string): boolean {
    const start = e.startsAt;
    const end = e.endsAt || start;
    return start <= dayIso && end >= dayIso;
  }

  private eventStatus(e: CalendarEvent): EventStatus {
    const end = e.endsAt || e.startsAt;
    return end < this.todayIso ? "past" : "upcoming";
  }

  private formatStart(iso: string): string | undefined {
    const d = isoToDate(iso);
    if (!d) {
      return undefined;
    }
    return `${d.getDate()}. ${d.getMonth() + 1}.`;
  }

  private formatShortDay(iso: string): string | undefined {
    const d = isoToDate(iso);
    if (!d) {
      return undefined;
    }
    const weekday = d.toLocaleDateString(LOCALE, { weekday: "short" });
    return `${weekday} ${d.getDate()}. ${d.getMonth() + 1}.`;
  }
}
