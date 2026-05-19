import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";

import { fmtCzk } from "./dashboard-data";
import type { AccessCard } from "../../../core/access-cards/access-cards.types";
import { ApiClient } from "../../../core/api/api-client";
import { dateToIso } from "../../../shared/date-iso";
import { type BillSummary, PaymentType } from "../../api/bills.types";
import {
  SpotState as ApiSpotState,
  type SpotStateRecord,
} from "../../api/spots.types";
import type { Vehicle } from "../../api/vehicles.types";

type MetricColor = "primary" | "arr" | "dep" | "guests" | "vehicles" | "money";

type BigMetric = {
  readonly id: string;
  readonly label: string;
  readonly value: string;
  readonly outOf?: string;
  readonly color: MetricColor;
  readonly icon: string;
};

const PLACEHOLDER = "—";

@Component({
  selector: "kemp-is-dash-metrics",
  imports: [],
  templateUrl: "./dashboard-metrics.html",
  styleUrl: "./dashboard-metrics.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashMetricsPanel {
  private readonly apiClient = inject(ApiClient);

  private readonly today = dateToIso(new Date());

  private readonly spotStates = httpResource<readonly SpotStateRecord[]>(() =>
    this.apiClient.url("/spots/states")
  );

  private readonly vehicles = httpResource<readonly Vehicle[]>(() =>
    this.apiClient.url(`/vehicles?from=${this.today}&to=${this.today}`)
  );

  private readonly guestsInCampCount = httpResource<number>(() =>
    this.apiClient.url("/guests/in-camp-count")
  );

  private readonly openBills = httpResource<readonly BillSummary[]>(() =>
    this.apiClient.url("/bills?closed=false")
  );

  private readonly accessCards = httpResource<readonly AccessCard[]>(() =>
    this.apiClient.url("/access-cards")
  );

  protected readonly metrics = computed<readonly BigMetric[]>(() => {
    const states = this.spotStates.hasValue() ? this.spotStates.value() : null;

    const occupied =
      states?.filter(
        s =>
          s.state === ApiSpotState.Occupied ||
          s.state === ApiSpotState.ExpectingDeparture
      ).length ?? null;
    const total = states?.length ?? null;
    const arrivalsPending =
      states?.filter(s => s.state === ApiSpotState.ExpectingArrival).length ??
      null;
    const departuresPending =
      states?.filter(s => s.state === ApiSpotState.ExpectingDeparture).length ??
      null;

    const vehicleCount = this.vehicles.hasValue()
      ? this.vehicles.value().length
      : null;
    const guestCount = this.guestsInCampCount.hasValue()
      ? this.guestsInCampCount.value()
      : null;
    const cashierTotal =
      this.openBills.hasValue() && this.accessCards.hasValue()
        ? this.openBills
            .value()
            .filter(
              b =>
                b.financialClosingId === null &&
                b.paymentType === PaymentType.Cash
            )
            .reduce((sum, b) => sum + b.amount, 0) +
          this.accessCards.value().reduce((sum, c) => sum + c.deposit, 0)
        : null;

    return [
      {
        id: "occupancy",
        label: "Obsazené chaty",
        value: occupied === null ? PLACEHOLDER : String(occupied),
        outOf: total === null ? undefined : String(total),
        color: "primary",
        icon: "pi-home",
      },
      {
        id: "guests",
        label: "Hostů v kempu",
        value: guestCount === null ? PLACEHOLDER : String(guestCount),
        color: "guests",
        icon: "pi-users",
      },
      {
        id: "vehicles",
        label: "Vozidel v kempu",
        value: vehicleCount === null ? PLACEHOLDER : String(vehicleCount),
        color: "vehicles",
        icon: "pi-car",
      },
      {
        id: "arrivals",
        label: "Zbývající příjezdy",
        value: arrivalsPending === null ? PLACEHOLDER : String(arrivalsPending),
        color: "arr",
        icon: "pi-sign-in",
      },
      {
        id: "departures",
        label: "Zbývající odjezdy",
        value:
          departuresPending === null ? PLACEHOLDER : String(departuresPending),
        color: "dep",
        icon: "pi-sign-out",
      },
      {
        id: "money",
        label: "Stav pokladny (Kč)",
        value: cashierTotal === null ? PLACEHOLDER : fmtCzk(cashierTotal),
        color: "money",
        icon: "pi-wallet",
      },
    ];
  });
}
