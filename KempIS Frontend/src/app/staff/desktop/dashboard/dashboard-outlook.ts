import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { ApiClient } from "../../../core/api/api-client";
import { SpotsStore } from "../../../core/spots/spots.store";
import { dateToIso } from "../../../shared/date-iso";
import {
  type Reservation,
  ReservationState,
} from "../../api/reservations.types";

type OutlookDay = {
  readonly day: string;
  readonly arrivals: number;
  readonly departures: number;
  readonly occupancyPct: number;
};

const LOCALE = "cs-CZ";
const DAYS = 7;
const MS_PER_DAY = 1000 * 60 * 60 * 24;

@Component({
  selector: "kemp-is-dash-outlook",
  imports: [],
  templateUrl: "./dashboard-outlook.html",
  styleUrl: "./dashboard-outlook.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashOutlookPanel {
  private readonly apiClient = inject(ApiClient);
  private readonly spotsStore = inject(SpotsStore);

  private readonly fromDate = ((): Date => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d;
  })();

  private readonly toDate = ((): Date => {
    const d = new Date(this.fromDate);
    d.setDate(d.getDate() + DAYS - 1);
    return d;
  })();

  private readonly fromIso = dateToIso(this.fromDate);
  private readonly toIso = dateToIso(this.toDate);

  private readonly reservations = httpResource<readonly Reservation[]>(() =>
    this.apiClient.url(`/reservations?from=${this.fromIso}&to=${this.toIso}`)
  );

  protected readonly outlook = computed<readonly OutlookDay[]>(() => {
    const list = this.reservations.hasValue() ? this.reservations.value() : [];
    const totalSpots = this.spotsStore.spots.hasValue()
      ? this.spotsStore.spots.value().filter(s => s.isActive).length
      : 0;

    const result: OutlookDay[] = [];
    for (let i = 0; i < DAYS; i++) {
      const day = new Date(this.fromDate.getTime() + i * MS_PER_DAY);
      const dayIso = dateToIso(day);
      const arrivals = list.filter(r => r.from === dayIso).length;
      const departures = list.filter(r => r.to === dayIso).length;
      const occupied = list.filter(
        r =>
          r.from <= dayIso &&
          r.to > dayIso &&
          (r.state === ReservationState.Confirmed ||
            r.state === ReservationState.CheckedIn)
      ).length;
      const occupancyPct =
        totalSpots === 0 ? 0 : Math.round((occupied / totalSpots) * 100);
      result.push({
        day: this.formatDay(day),
        arrivals,
        departures,
        occupancyPct,
      });
    }
    return result;
  });

  private formatDay(d: Date): string {
    const weekday = d.toLocaleDateString(LOCALE, { weekday: "short" });
    return `${weekday} ${d.getDate()}. ${d.getMonth() + 1}.`;
  }
}
