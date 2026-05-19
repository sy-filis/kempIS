import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { Router } from "@angular/router";

import { TagModule } from "primeng/tag";

import { ApiClient } from "../../../core/api/api-client";
import { SpotsStore } from "../../../core/spots/spots.store";
import { dateToIso } from "../../../shared/date-iso";
import {
  type Reservation,
  ReservationState,
} from "../../api/reservations.types";

type ArrivalRow = {
  readonly id: string;
  readonly ref: string;
  readonly name: string;
  readonly unit: string;
  readonly status: "pending" | "arrived";
};

type DepartureRow = {
  readonly id: string;
  readonly ref: string;
  readonly name: string;
  readonly unit: string;
  readonly status: "pending" | "done";
};

@Component({
  selector: "kemp-is-dash-arrivals",
  imports: [TagModule],
  templateUrl: "./dashboard-arrivals.html",
  styleUrl: "./dashboard-arrivals.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashArrivalsPanel {
  private readonly apiClient = inject(ApiClient);
  private readonly spotsStore = inject(SpotsStore);
  private readonly router = inject(Router);

  private readonly today = dateToIso(new Date());

  private readonly reservations = httpResource<readonly Reservation[]>(() =>
    this.apiClient.url(`/reservations?from=${this.today}&to=${this.today}`)
  );

  protected readonly arrivals = computed<readonly ArrivalRow[]>(() => {
    const list = this.reservations.hasValue() ? this.reservations.value() : [];
    return list
      .filter(
        r =>
          r.from === this.today &&
          (r.state === ReservationState.Confirmed ||
            r.state === ReservationState.CheckedIn)
      )
      .map(r => ({
        id: r.id,
        ref: r.number,
        name: this.makerName(r),
        unit: this.unitLabel(r),
        status: r.state === ReservationState.CheckedIn ? "arrived" : "pending",
      }));
  });

  protected readonly departures = computed<readonly DepartureRow[]>(() => {
    const list = this.reservations.hasValue() ? this.reservations.value() : [];
    return list
      .filter(
        r =>
          r.to === this.today &&
          (r.state === ReservationState.CheckedIn ||
            r.state === ReservationState.Completed)
      )
      .map(r => ({
        id: r.id,
        ref: r.number,
        name: this.makerName(r),
        unit: this.unitLabel(r),
        status: r.state === ReservationState.Completed ? "done" : "pending",
      }));
  });

  protected onOpen(id: string): void {
    void this.router.navigate(["/staff/auth/desktop/reservations", id, "edit"]);
  }

  private makerName(r: Reservation): string {
    return `${r.reservationMakerSurname} ${r.reservationMakerName}`.trim();
  }

  private unitLabel(r: Reservation): string {
    if (r.spotItems.length === 0) {
      return "—";
    }
    const names = r.spotItems
      .map(id => this.spotsStore.nameOf(id))
      .filter(n => n !== "—");
    if (names.length === 0) {
      return "—";
    }
    if (names.length === 1) {
      return names[0]!;
    }
    return `${names[0]} +${names.length - 1}`;
  }
}
