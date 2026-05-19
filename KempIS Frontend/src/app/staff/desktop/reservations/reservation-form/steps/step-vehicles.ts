import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  model,
  signal,
  ViewEncapsulation,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";

import { type MenuItem, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MenuModule } from "primeng/menu";
import { SelectModule } from "primeng/select";

import { ServicesStore } from "../../../../../core/services/services.store";
import { VehiclesApi } from "../../../../api/vehicles.api";
import type { VehicleRequest } from "../../../../api/vehicles.types";
import { ServiceGroup } from "../../../system-settings/shared/service-groups";
import {
  type Tent,
  type Vehicle,
  type VehicleType,
} from "../reservation-form-stub-data";

@Component({
  selector: "kemp-is-reservation-step-vehicles",
  imports: [
    FormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    MenuModule,
    SelectModule,
  ],
  templateUrl: "./step-vehicles.html",
  styleUrl: "./step-vehicles.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  host: {
    "(document:keydown.escape)": "onEscape()",
  },
})
export class StepVehicles {
  private readonly servicesStore = inject(ServicesStore);
  private readonly vehiclesApi = inject(VehiclesApi);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly nights = input<number>(1);

  // null while creating a new reservation; vehicle rows are then local-only
  // and persisted via the broader POST /reservations on submit.
  readonly reservationId = input<string | null>(null);

  // Debounce timers per vehicle id so plate keystrokes collapse into one PUT.
  private readonly pendingPlateTimers = new Map<
    string,
    ReturnType<typeof setTimeout>
  >();

  // Visual split into "Vozidla" / "Karavany" is purely UI, derived from
  // each row's serviceId group lookup.
  readonly vehicles = model<readonly Vehicle[]>([]);

  protected readonly vehicleTypes = computed<VehicleType[]>(() =>
    this.servicesStore
      .byGroup(ServiceGroup.Vehicles)
      .map(s => ({ id: s.id, label: s.name, rate: s.basePrice }))
  );
  protected readonly caravanTypes = computed<VehicleType[]>(() =>
    this.servicesStore
      .byGroup(ServiceGroup.MotorHomes)
      .map(s => ({ id: s.id, label: s.name, rate: s.basePrice }))
  );

  private readonly vehicleServiceIds = computed<ReadonlySet<string>>(
    () => new Set(this.vehicleTypes().map(t => t.id))
  );

  private readonly caravanServiceIds = computed<ReadonlySet<string>>(
    () => new Set(this.caravanTypes().map(t => t.id))
  );

  // step-services and step-vehicles share this map; each step renders
  // only entries whose service belongs to its own group bucket.
  readonly tentQtys = model<ReadonlyMap<string, number>>(new Map());

  protected readonly tents = computed<readonly Tent[]>(() => {
    const qtys = this.tentQtys();
    return this.servicesStore.byGroup(ServiceGroup.Tents).map(s => ({
      id: s.id,
      label: s.name,
      ratePerNight: s.basePrice,
      qty: qtys.get(s.id) ?? 0,
      nights: this.nights(),
    }));
  });

  protected readonly editingId = signal<string | null>(null);

  protected readonly vehicleRows = computed(() => {
    const ids = this.vehicleServiceIds();
    return this.vehicles()
      .filter(v => ids.has(v.serviceId))
      .map(v => ({ v, total: v.nights * v.ratePerNight }));
  });

  // Vehicles persisted without a service link (walk-in / parking-lot
  // rows). Surfaced separately so the user can pick a concrete service.
  protected readonly unassignedRows = computed(() =>
    this.vehicles().filter(v => v.serviceId.length === 0)
  );

  protected readonly allVehicleTypes = computed<VehicleType[]>(() => [
    ...this.vehicleTypes(),
    ...this.caravanTypes(),
  ]);

  protected readonly caravanRows = computed(() => {
    const ids = this.caravanServiceIds();
    return this.vehicles()
      .filter(v => ids.has(v.serviceId))
      .map(v => ({ v, total: v.nights * v.ratePerNight }));
  });

  protected readonly tentTotalQty = computed(() =>
    this.tents().reduce((sum, t) => sum + t.qty, 0)
  );

  protected readonly carsTotal = computed(() =>
    this.vehicleRows().reduce((s, r) => s + r.total, 0)
  );
  protected readonly caravansTotal = computed(() =>
    this.caravanRows().reduce((s, r) => s + r.total, 0)
  );
  protected readonly tentsTotal = computed(() =>
    this.tents().reduce((s, t) => s + t.qty * t.nights * t.ratePerNight, 0)
  );
  protected readonly grandTotal = computed(
    () => this.carsTotal() + this.caravansTotal() + this.tentsTotal()
  );

  protected readonly vehicleMenuItems = computed<MenuItem[]>(() =>
    this.vehicleTypes().map(t => ({
      label: `${t.label} (${t.rate} Kč)`,
      icon: "pi pi-plus",
      command: (): void => this.addVehicle(t),
    }))
  );

  protected readonly caravanMenuItems = computed<MenuItem[]>(() =>
    this.caravanTypes().map(t => ({
      label: `${t.label} (${t.rate} Kč)`,
      icon: "pi pi-plus",
      command: (): void => this.addCaravan(t),
    }))
  );

  protected addVehicle(t: VehicleType): void {
    const v = this.makeVehicle(t);
    this.vehicles.update(list => [...list, v]);
    this.editingId.set(v.id);
  }

  protected addCaravan(t: VehicleType): void {
    this.addVehicle(t);
  }

  protected deleteVehicle(id: string): void {
    const row = this.vehicles().find(v => v.id === id);
    this.vehicles.update(list => list.filter(v => v.id !== id));
    if (this.editingId() === id) {
      this.editingId.set(null);
    }
    this.cancelPlateSync(id);
    if (row && row.persistentId !== null) {
      this.deleteRemote(row.persistentId);
    }
  }

  protected deleteCaravan(id: string): void {
    this.deleteVehicle(id);
  }

  protected startEdit(id: string): void {
    this.editingId.set(id);
  }

  protected stopEdit(): void {
    this.editingId.set(null);
  }

  protected onEscape(): void {
    if (this.editingId() !== null) {
      this.stopEdit();
    }
  }

  protected updateVehicleField<K extends keyof Vehicle>(
    id: string,
    field: K,
    value: Vehicle[K]
  ): void {
    this.vehicles.update(list =>
      list.map(v => (v.id === id ? { ...v, [field]: value } : v))
    );
    if (field === "plate") {
      this.schedulePlateSync(id);
    }
  }

  protected changeVehicleType(id: string, typeId: string): void {
    const t =
      this.vehicleTypes().find(x => x.id === typeId) ??
      this.caravanTypes().find(x => x.id === typeId);
    if (!t) {
      return;
    }
    this.vehicles.update(list =>
      list.map(v =>
        v.id === id
          ? { ...v, type: t.label, serviceId: t.id, ratePerNight: t.rate }
          : v
      )
    );
    this.syncVehicleNow(id);
  }

  protected updateTentQty(id: string, qty: number): void {
    this.tentQtys.update(m => {
      const next = new Map(m);
      next.set(id, Math.max(0, qty));
      return next;
    });
  }

  protected tentLineTotal(tent: Tent): number {
    return tent.qty * tent.nights * tent.ratePerNight;
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }

  private makeVehicle(t: VehicleType): Vehicle {
    return {
      id: crypto.randomUUID() as string,
      persistentId: null,
      plate: "",
      type: t.label,
      serviceId: t.id,
      nights: this.nights(),
      ratePerNight: t.rate,
    };
  }

  // Debounce 400ms so a typed plate doesn't fire one PUT per character.
  private schedulePlateSync(id: string): void {
    const existing = this.pendingPlateTimers.get(id);
    if (existing !== undefined) {
      clearTimeout(existing);
    }
    const handle = setTimeout(() => {
      this.pendingPlateTimers.delete(id);
      this.syncVehicleNow(id);
    }, 400);
    this.pendingPlateTimers.set(id, handle);
  }

  private cancelPlateSync(id: string): void {
    const handle = this.pendingPlateTimers.get(id);
    if (handle !== undefined) {
      clearTimeout(handle);
      this.pendingPlateTimers.delete(id);
    }
  }

  // No-op for rows without a persistent id; those land via POST/PUT /reservations.
  private syncVehicleNow(id: string): void {
    const row = this.vehicles().find(v => v.id === id);
    if (!row || row.persistentId === null) {
      return;
    }
    const request: VehicleRequest = {
      reservationId: this.reservationId(),
      billId: null,
      serviceId: row.serviceId.length > 0 ? row.serviceId : null,
      registrationNumber: row.plate,
    };
    this.vehiclesApi
      .update(row.persistentId, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: () => {
          this.messageService.add({
            severity: "error",
            summary: "Vozidlo",
            detail: "Změnu se nepodařilo uložit.",
          });
        },
      });
  }

  private deleteRemote(persistentId: string): void {
    this.vehiclesApi
      .delete(persistentId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        error: () => {
          this.messageService.add({
            severity: "error",
            summary: "Vozidlo",
            detail: "Vozidlo se nepodařilo smazat.",
          });
        },
      });
  }
}
