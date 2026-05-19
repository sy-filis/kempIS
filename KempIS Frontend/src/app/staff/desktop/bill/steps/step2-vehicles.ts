import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";

import { type MenuItem, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MenuModule } from "primeng/menu";
import { SelectModule } from "primeng/select";

import { ServicesStore } from "../../../../core/services/services.store";
import { VehiclesApi } from "../../../api/vehicles.api";
import type { VehicleRequest } from "../../../api/vehicles.types";
import { ServiceGroup } from "../../system-settings/shared/service-groups";
import { type Tent, type Vehicle, type VehicleType } from "../bill-data";
import { BillState } from "../bill-state";

@Component({
  selector: "kemp-is-bill-step2-vehicles",
  imports: [
    FormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    MenuModule,
    SelectModule,
  ],
  templateUrl: "./step2-vehicles.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    "(document:keydown.escape)": "onEscape()",
  },
})
export class Step2Vehicles {
  private readonly servicesStore = inject(ServicesStore);
  private readonly billState = inject(BillState);
  private readonly vehiclesApi = inject(VehiclesApi);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  /** Passed through to PUT /vehicles/{id} so the backend preserves
   *  the existing linkage on vehicle edits. New rows are persisted via
   *  POST /bills at submit time. */
  readonly reservationId = input<string | null>(null);
  readonly billId = input<string | null>(null);

  private readonly pendingPlateTimers = new Map<
    string,
    ReturnType<typeof setTimeout>
  >();

  protected readonly nights = this.billState.nights;

  protected readonly vehicles = this.billState.vehicles;
  protected readonly caravans = this.billState.caravans;

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

  private readonly tentQtys = this.billState.tentQtys;

  protected readonly tents = computed<readonly Tent[]>(() => {
    const qtys = this.tentQtys();
    const nights = Math.max(1, this.billState.nights());
    return this.servicesStore.byGroup(ServiceGroup.Tents).map(s => ({
      id: s.id,
      label: s.name,
      ratePerNight: s.basePrice,
      qty: qtys.get(s.id) ?? 0,
      nights,
    }));
  });

  constructor() {
    effect(() => {
      this.billState.tents.set(this.tents());
    });
  }

  protected readonly editingId = signal<string | null>(null);

  private nextId = 1000;

  protected readonly vehicleRows = computed(() =>
    this.vehicles().map(v => ({ v, total: v.nights * v.ratePerNight }))
  );
  protected readonly caravanRows = computed(() =>
    this.caravans().map(v => ({ v, total: v.nights * v.ratePerNight }))
  );

  protected readonly tentTotalQty = computed(() =>
    this.tents().reduce((sum, t) => sum + t.qty, 0)
  );

  protected readonly carsTotal = computed(() =>
    this.vehicles().reduce((s, v) => s + v.nights * v.ratePerNight, 0)
  );
  protected readonly caravansTotal = computed(() =>
    this.caravans().reduce((s, v) => s + v.nights * v.ratePerNight, 0)
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
    const v = this.makeVehicle(t, "V");
    this.vehicles.update(list => [...list, v]);
    this.editingId.set(v.id);
  }

  protected addCaravan(t: VehicleType): void {
    const v = this.makeVehicle(t, "C");
    this.caravans.update(list => [...list, v]);
    this.editingId.set(v.id);
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
    const row = this.caravans().find(v => v.id === id);
    this.caravans.update(list => list.filter(v => v.id !== id));
    if (this.editingId() === id) {
      this.editingId.set(null);
    }
    this.cancelPlateSync(id);
    if (row && row.persistentId !== null) {
      this.deleteRemote(row.persistentId);
    }
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
    bucket: "vehicles" | "caravans",
    id: string,
    field: K,
    value: Vehicle[K]
  ): void {
    const target = bucket === "vehicles" ? this.vehicles : this.caravans;
    target.update(list =>
      list.map(v => (v.id === id ? { ...v, [field]: value } : v))
    );
    if (field === "plate") {
      this.schedulePlateSync(bucket, id);
    }
  }

  protected changeVehicleType(
    bucket: "vehicles" | "caravans",
    id: string,
    typeId: string
  ): void {
    const types =
      bucket === "vehicles" ? this.vehicleTypes() : this.caravanTypes();
    const t = types.find(x => x.id === typeId);
    if (!t) {
      return;
    }
    const target = bucket === "vehicles" ? this.vehicles : this.caravans;
    target.update(list =>
      list.map(v =>
        v.id === id
          ? { ...v, type: t.label, serviceId: t.id, ratePerNight: t.rate }
          : v
      )
    );
    this.syncBucketRow(bucket, id);
  }

  protected typeIdFor(bucket: "vehicles" | "caravans", label: string): string {
    const types =
      bucket === "vehicles" ? this.vehicleTypes() : this.caravanTypes();
    return types.find(t => t.label === label)?.id ?? types[0]?.id ?? "";
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

  private makeVehicle(t: VehicleType, prefix: string): Vehicle {
    this.nextId += 1;
    return {
      id: `${prefix}${this.nextId}`,
      persistentId: null,
      plate: "",
      type: t.label,
      serviceId: t.id,
      nights: Math.max(1, this.billState.nights()),
      ratePerNight: t.rate,
    };
  }

  protected readonly unassignedVehicles = this.billState.unassignedVehicles;

  protected readonly allVehicleTypes = computed<VehicleType[]>(() => [
    ...this.vehicleTypes(),
    ...this.caravanTypes(),
  ]);

  protected updateUnassignedPlate(id: string, plate: string): void {
    this.billState.unassignedVehicles.update(list =>
      list.map(v => (v.id === id ? { ...v, plate } : v))
    );
    this.schedulePlateSync("unassigned", id);
  }

  protected deleteUnassigned(id: string): void {
    const row = this.unassignedVehicles().find(v => v.id === id);
    this.billState.unassignedVehicles.update(list =>
      list.filter(v => v.id !== id)
    );
    this.cancelPlateSync(id);
    if (row && row.persistentId !== null) {
      this.deleteRemote(row.persistentId);
    }
  }

  protected assignUnassignedService(id: string, typeId: string): void {
    const asVehicle = this.vehicleTypes().find(t => t.id === typeId);
    const asCaravan = asVehicle
      ? null
      : (this.caravanTypes().find(t => t.id === typeId) ?? null);
    const t = asVehicle ?? asCaravan;
    if (!t) {
      return;
    }
    const row = this.unassignedVehicles().find(v => v.id === id);
    if (!row) {
      return;
    }
    const migrated: Vehicle = {
      ...row,
      type: t.label,
      serviceId: t.id,
      ratePerNight: t.rate,
      nights: Math.max(1, this.billState.nights()),
    };
    this.billState.unassignedVehicles.update(list =>
      list.filter(v => v.id !== id)
    );
    const targetBucket: "vehicles" | "caravans" = asVehicle
      ? "vehicles"
      : "caravans";
    if (asVehicle) {
      this.vehicles.update(list => [...list, migrated]);
    } else {
      this.caravans.update(list => [...list, migrated]);
    }
    this.syncBucketRow(targetBucket, migrated.id);
  }

  private schedulePlateSync(
    bucket: "vehicles" | "caravans" | "unassigned",
    id: string
  ): void {
    const existing = this.pendingPlateTimers.get(id);
    if (existing !== undefined) {
      clearTimeout(existing);
    }
    const handle = setTimeout(() => {
      this.pendingPlateTimers.delete(id);
      this.syncBucketRow(bucket, id);
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

  private findRow(
    bucket: "vehicles" | "caravans" | "unassigned",
    id: string
  ): Vehicle | undefined {
    if (bucket === "vehicles") {
      return this.vehicles().find(v => v.id === id);
    }
    if (bucket === "caravans") {
      return this.caravans().find(v => v.id === id);
    }
    return this.unassignedVehicles().find(v => v.id === id);
  }

  private syncBucketRow(
    bucket: "vehicles" | "caravans" | "unassigned",
    id: string
  ): void {
    const row = this.findRow(bucket, id);
    if (!row || row.persistentId === null) {
      return;
    }
    const request: VehicleRequest = {
      reservationId: this.reservationId(),
      billId: this.billId(),
      serviceId: row.serviceId,
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
