import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
} from "@angular/core";

import { ServicesStore } from "../../../../core/services/services.store";
import { VatRatesStore } from "../../../../core/vat-rates/vat-rates.store";
import { StepServices } from "../../reservations/reservation-form/steps/step-services";
import { ServiceGroup } from "../../system-settings/shared/service-groups";
import { BillState } from "../bill-state";

// Recreation fees are billed via step 1's per-guest toggle, excluded
// here to avoid double-charging.
const BASE_GROUPS: readonly ServiceGroup[] = [
  ServiceGroup.Persons,
  ServiceGroup.Others,
];

// Spots and meals have dedicated steps when linked to a reservation;
// absorb them here for standalone bills.
const SPOT_AND_MEAL_GROUPS: readonly ServiceGroup[] = [
  ServiceGroup.Spots,
  ServiceGroup.Meals,
];

@Component({
  selector: "kemp-is-bill-step6-other",
  imports: [StepServices],
  templateUrl: "./step6-other.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step6Other {
  readonly hasReservation = input<boolean>(true);

  private readonly billState = inject(BillState);
  private readonly servicesStore = inject(ServicesStore);
  private readonly vatRatesStore = inject(VatRatesStore);

  protected readonly groups = computed<readonly ServiceGroup[]>(() =>
    this.hasReservation()
      ? BASE_GROUPS
      : [...BASE_GROUPS, ...SPOT_AND_MEAL_GROUPS]
  );

  protected readonly quantities = this.billState.serviceQtys;

  constructor() {
    effect(() => {
      const groups = new Set(this.groups());
      const rateById = this.vatRatesStore.rateById();
      const map = new Map<
        string,
        {
          name: string;
          basePrice: number;
          vatRate: number;
          serviceGroup: ServiceGroup;
        }
      >();
      for (const s of this.servicesStore.active()) {
        if (!groups.has(s.serviceGroup)) {
          continue;
        }
        map.set(s.id, {
          name: s.name,
          basePrice: s.basePrice,
          vatRate: rateById.get(s.vatRateId) ?? 0,
          serviceGroup: s.serviceGroup,
        });
      }
      this.billState.serviceCatalogue.set(map);
    });
  }
}
