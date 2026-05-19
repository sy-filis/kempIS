import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  model,
  ViewEncapsulation,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";

import { KNOWN_SERVICE_IDS } from "../../../../../core/services/known-service-ids";
import { ServicesStore } from "../../../../../core/services/services.store";
import {
  SERVICE_GROUP_LABELS,
  ServiceGroup,
} from "../../../system-settings/shared/service-groups";
import type { Service } from "../../../system-settings/shared/types";

// Everything except meals, spots, vehicles, motorhomes and tents (each
// of which has its own dedicated step). Order matches staff presentation.
const DEFAULT_SERVICE_GROUPS: readonly ServiceGroup[] = [
  ServiceGroup.Persons,
  ServiceGroup.RecreationFees,
  ServiceGroup.Others,
];

const KNOWN_HANDLED_IDS: ReadonlySet<string> = new Set(
  Object.values(KNOWN_SERVICE_IDS)
);

type ServiceRowView = {
  readonly service: Service;
  readonly qty: number;
  readonly lineTotal: number;
};

type ServiceGroupView = {
  readonly group: ServiceGroup;
  readonly label: string;
  readonly rows: readonly ServiceRowView[];
  readonly subtotal: number;
};

@Component({
  selector: "kemp-is-reservation-step-services",
  imports: [FormsModule, ButtonModule, InputNumberModule],
  templateUrl: "./step-services.html",
  styleUrl: "./step-services.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StepServices {
  private readonly servicesStore = inject(ServicesStore);

  // Zero entries are absent from the map (sparse).
  readonly quantities = model<ReadonlyMap<string, number>>(new Map());

  // Bill flow passes a narrower list (just Others); adult/child fees
  // and recreation fees are collected in dedicated steps there.
  readonly groups = input<readonly ServiceGroup[]>(DEFAULT_SERVICE_GROUPS);

  readonly excludeKnown = input<boolean>(false);
  readonly showTitle = input<boolean>(true);
  readonly showSubtotal = input<boolean>(true);

  protected readonly groupViews = computed<ServiceGroupView[]>(() => {
    const qtys = this.quantities();
    const skipKnown = this.excludeKnown();
    return this.groups()
      .map<ServiceGroupView>(group => {
        const services = [...this.servicesStore.byGroup(group)]
          .filter(s => !skipKnown || !KNOWN_HANDLED_IDS.has(s.id))
          .sort((a, b) => a.name.localeCompare(b.name, "cs"));
        const rows = services.map<ServiceRowView>(service => {
          const qty = qtys.get(service.id) ?? 0;
          return {
            service,
            qty,
            lineTotal: qty * service.basePrice,
          };
        });
        const subtotal = rows.reduce((sum, r) => sum + r.lineTotal, 0);
        return {
          group,
          label: SERVICE_GROUP_LABELS[group],
          rows,
          subtotal,
        };
      })
      .filter(view => view.rows.length > 0);
  });

  protected readonly grandTotal = computed<number>(() =>
    this.groupViews().reduce((sum, gv) => sum + gv.subtotal, 0)
  );

  protected readonly anyAssigned = computed<boolean>(() =>
    this.groupViews().some(gv => gv.rows.some(r => r.qty > 0))
  );

  protected updateQty(serviceId: string, qty: number): void {
    this.quantities.update(prev => {
      const next = new Map(prev);
      const sanitized = Math.max(0, Math.floor(qty));
      if (sanitized === 0) {
        next.delete(serviceId);
      } else {
        next.set(serviceId, sanitized);
      }
      return next;
    });
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
}
