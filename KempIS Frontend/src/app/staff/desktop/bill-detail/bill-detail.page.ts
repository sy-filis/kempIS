import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { CardModule } from "primeng/card";
import { DialogModule } from "primeng/dialog";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { ProgressSpinnerModule } from "primeng/progressspinner";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { ApiClient } from "../../../core/api/api-client";
import { NationalitiesStore } from "../../../core/nationalities/nationalities.store";
import { ServicesStore } from "../../../core/services/services.store";
import { SpotGroupsStore } from "../../../core/spots/spot-groups.store";
import { SpotsStore } from "../../../core/spots/spots.store";
import { BillsApi } from "../../api/bills.api";
import {
  type BillAddress,
  type BillDetail,
  type BillItemView,
  BillKind,
  PaymentType,
} from "../../api/bills.types";
import {
  GuestDocumentType,
  type ReservationDetail,
  type ReservationDetailGuest,
} from "../../api/reservations.types";
import {
  GuestEditorDialogComponent,
  type GuestEditorInput,
} from "../guest-editor-dialog/guest-editor-dialog.component";

type ItemRow = {
  readonly id: string;
  readonly serviceName: string;
  readonly recapSingleQuantity: number;
  readonly recapDayQuantity: number;
  readonly unitPrice: number;
  readonly vatRatePercentage: number;
  readonly lineTotal: number;
};

type LinkedSpotRow = {
  readonly itemId: string;
  readonly spotName: string;
  readonly groupName: string;
};

type LinkedVehicleRow = {
  readonly id: string;
  readonly registrationNumber: string;
  readonly serviceName: string;
};

/** `full` is null for guests created on a standalone bill; the dialog
 *  falls back to the basic view and disables the editor in that case. */
type EnrichedGuest = {
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly paysRecreationFee: boolean | null;
  readonly full: ReservationDetailGuest | null;
};

type RepairLineDraft = {
  readonly id: string;
  readonly serviceId: string | null;
  readonly serviceName: string;
  readonly unitPrice: number;
  readonly vatRatePercentage: number;
  readonly recapSingleQuantity: number;
  readonly recapDayQuantity: number;
  qty: number;
};

const CZK = new Intl.NumberFormat("cs-CZ", {
  style: "currency",
  currency: "CZK",
  maximumFractionDigits: 0,
});

const CZK2 = new Intl.NumberFormat("cs-CZ", {
  style: "currency",
  currency: "CZK",
  maximumFractionDigits: 2,
});

@Component({
  selector: "kemp-is-bill-detail",
  imports: [
    FormsModule,
    ButtonModule,
    CardModule,
    DialogModule,
    GuestEditorDialogComponent,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    ProgressSpinnerModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
  ],
  templateUrl: "./bill-detail.page.html",
  styleUrl: "./bill-detail.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class BillDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly apiClient = inject(ApiClient);
  private readonly billsApi = inject(BillsApi);
  private readonly servicesStore = inject(ServicesStore);
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly spotsStore = inject(SpotsStore);
  private readonly spotGroupsStore = inject(SpotGroupsStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messages = inject(MessageService);

  protected readonly viewing = signal<boolean>(false);
  protected readonly repairOpen = signal<boolean>(false);
  protected readonly repairSubmitting = signal<boolean>(false);
  protected readonly repairLines = signal<readonly RepairLineDraft[]>([]);
  protected readonly repairPaymentType = signal<PaymentType>(PaymentType.Card);
  protected readonly repairReason = signal<string>("");

  protected readonly paymentTypeOptions = [
    { id: PaymentType.Cash, name: "Hotově" },
    { id: PaymentType.Card, name: "Kartou" },
  ];

  protected readonly guestDialogVisible = signal<boolean>(false);

  protected readonly editingGuestId = signal<string | null>(null);

  private readonly routeParams = toSignal(this.route.paramMap, {
    requireSync: true,
  });

  protected readonly billId = computed<string | null>(() =>
    this.routeParams().get("id")
  );

  protected readonly resource = httpResource<BillDetail>(() => {
    const id = this.billId();
    return id
      ? this.apiClient.url(`/bills/${encodeURIComponent(id)}`)
      : undefined;
  });

  protected readonly loading = computed(() => this.resource.isLoading());
  protected readonly errorMessage = computed(() => {
    const err = this.resource.error();
    if (err === undefined) {
      return null;
    }
    return err instanceof Error ? err.message : "Načtení účtenky selhalo.";
  });

  protected readonly bill = computed<BillDetail | null>(() =>
    this.resource.hasValue() ? this.resource.value() : null
  );

  protected readonly reservationResource = httpResource<ReservationDetail>(
    () => {
      const id = this.bill()?.reservationId;
      return id
        ? this.apiClient.url(`/reservations/${encodeURIComponent(id)}`)
        : undefined;
    }
  );

  protected readonly reservation = computed<ReservationDetail | null>(() =>
    this.reservationResource.hasValue()
      ? this.reservationResource.value()
      : null
  );

  protected readonly originalBillResource = httpResource<BillDetail>(() => {
    const id = this.bill()?.originalBillId ?? null;
    return id
      ? this.apiClient.url(`/bills/${encodeURIComponent(id)}`)
      : undefined;
  });

  protected readonly originalBillSummary = computed<{
    id: string;
    number: string;
  } | null>(() => {
    if (!this.originalBillResource.hasValue()) {
      return null;
    }
    const r = this.originalBillResource.value();
    return { id: r.id, number: r.number };
  });

  protected readonly hasOriginalBill = computed(
    () => (this.bill()?.originalBillId ?? null) !== null
  );

  protected onOpenOriginalBill(): void {
    const id = this.bill()?.originalBillId;
    if (id) {
      void this.router.navigate(["/staff/auth/desktop/bills", id]);
    }
  }

  protected readonly itemRows = computed<ItemRow[]>(() => {
    const b = this.bill();
    if (!b) {
      return [];
    }
    return b.items.map(i => this.toRow(i));
  });

  protected readonly itemsTotal = computed<number>(() =>
    this.itemRows().reduce((sum, r) => sum + r.lineTotal, 0)
  );

  /** Items hold VAT-inclusive unit prices; base is backed out as
   *  `gross / (1 + rate/100)`, tax is the remainder. */
  protected readonly vatBreakdown = computed<
    readonly { rate: number; base: number; tax: number; total: number }[]
  >(() => {
    const grossByRate = new Map<number, number>();
    for (const row of this.itemRows()) {
      grossByRate.set(
        row.vatRatePercentage,
        (grossByRate.get(row.vatRatePercentage) ?? 0) + row.lineTotal
      );
    }
    return [...grossByRate.entries()]
      .sort(([a], [b]) => a - b)
      .map(([rate, total]) => {
        const base = total / (1 + rate / 100);
        const tax = total - base;
        return { rate, base, tax, total };
      });
  });

  protected readonly vatTotals = computed<{
    base: number;
    tax: number;
    total: number;
  }>(() => {
    const summary = { base: 0, tax: 0, total: 0 };
    for (const row of this.vatBreakdown()) {
      summary.base += row.base;
      summary.tax += row.tax;
      summary.total += row.total;
    }
    return summary;
  });

  protected readonly deductionsTotal = computed<number>(() => {
    const b = this.bill();
    if (!b) {
      return 0;
    }
    return b.deductions.reduce((sum, d) => sum + d.amount, 0);
  });

  protected readonly hasRepairs = computed(
    () => (this.bill()?.repairs.length ?? 0) > 0
  );

  protected readonly hasDeductions = computed(
    () => (this.bill()?.deductions.length ?? 0) > 0
  );

  protected readonly hasLegalEntity = computed(
    () =>
      this.bill()?.legalEntity !== null &&
      this.bill()?.legalEntity !== undefined
  );

  protected readonly hasGuests = computed(
    () => (this.bill()?.guests.length ?? 0) > 0
  );

  /** Backend enforces "repair against Regular only" with a 409. */
  protected readonly canCreateRepair = computed<boolean>(
    () => this.bill()?.kind === BillKind.Regular
  );

  protected readonly canDuplicate = computed<boolean>(
    () => (this.bill()?.reservationId ?? null) === null
  );

  protected onDuplicate(): void {
    const id = this.billId();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/bill/new"], {
      queryParams: { duplicateFromBillId: id },
    });
  }

  protected readonly repairTotal = computed<number>(() =>
    this.repairLines().reduce((sum, line) => sum + line.qty * line.unitPrice, 0)
  );

  protected readonly hasRepairSelection = computed<boolean>(() =>
    this.repairLines().some(line => line.qty > 0)
  );

  protected readonly linkedSpots = computed<readonly LinkedSpotRow[]>(() => {
    const billId = this.billId();
    const r = this.reservation();
    if (!billId || !r) {
      return [];
    }
    if (!this.spotsStore.spots.hasValue()) {
      return [];
    }
    const spotsById = new Map(
      this.spotsStore.spots.value().map(s => [s.id, s])
    );
    const groupsById = this.spotGroupsStore.byId();
    const collator = new Intl.Collator("cs", { numeric: true });
    const rows: LinkedSpotRow[] = [];
    for (const item of r.spotItems) {
      if (item.billId !== billId || item.spotId === null) {
        continue;
      }
      const spot = spotsById.get(item.spotId);
      const group = spot ? groupsById.get(spot.spotGroupId) : null;
      rows.push({
        itemId: item.id,
        spotName: spot?.name ?? "—",
        groupName: group?.name ?? "",
      });
    }
    return rows.sort((a, b) => collator.compare(a.spotName, b.spotName));
  });

  protected readonly linkedVehicles = computed<readonly LinkedVehicleRow[]>(
    () => {
      const billId = this.billId();
      const r = this.reservation();
      if (!billId || !r) {
        return [];
      }
      return r.vehicles
        .filter(v => v.billId === billId)
        .map(v => ({
          id: v.id,
          registrationNumber: v.registrationNumber,
          serviceName: v.serviceId
            ? (this.servicesStore.byId(v.serviceId)?.name ?? "")
            : "",
        }));
    }
  );

  protected readonly enrichedGuests = computed<readonly EnrichedGuest[]>(() => {
    const b = this.bill();
    if (!b) {
      return [];
    }
    const fullById = new Map(
      (this.reservation()?.guests ?? []).map(g => [g.id, g])
    );
    return b.guests.map(g => ({
      id: g.id,
      firstName: g.firstName,
      lastName: g.lastName,
      paysRecreationFee: g.paysRecreationFee,
      full: fullById.get(g.id) ?? null,
    }));
  });

  protected readonly hasReservationLink = computed(
    () => (this.bill()?.reservationId ?? null) !== null
  );

  protected readonly reservationSummary = computed<{
    number: string;
    maker: string;
  } | null>(() => {
    const r = this.reservation();
    if (!r) {
      return null;
    }
    const maker = [r.reservationMakerName, r.reservationMakerSurname]
      .filter(s => s.length > 0)
      .join(" ");
    return { number: r.number, maker };
  });

  private readonly documentTypeNames: ReadonlyMap<GuestDocumentType, string> =
    new Map([
      [GuestDocumentType.IdCard, "Občanský průkaz"],
      [GuestDocumentType.Passport, "Cestovní pas"],
      [GuestDocumentType.CzechResidencePermit, "Povolení k pobytu (CZ)"],
      [GuestDocumentType.LostPassportConfirmation, "Potvrzení o ztrátě pasu"],
      [GuestDocumentType.CzechDiplomatCard, "Diplomatická karta (CZ)"],
      [GuestDocumentType.ChildInParentPassport, "Dítě zapsané v pase rodiče"],
    ]);

  protected readonly editingGuest = computed<EnrichedGuest | null>(() => {
    const id = this.editingGuestId();
    if (!id) {
      return null;
    }
    return this.enrichedGuests().find(g => g.id === id) ?? null;
  });

  protected readonly guestEditorInput = computed<GuestEditorInput | null>(
    () => {
      const g = this.editingGuest();
      if (!g) {
        return null;
      }
      const bill = this.bill();
      const reservationId = bill?.reservationId ?? null;
      return {
        id: g.id,
        reservationId,
        billId: bill?.id ?? null,
        firstName: g.firstName,
        lastName: g.lastName,
        paysRecreationFee: g.paysRecreationFee,
        full: g.full
          ? {
              nationalityId: g.full.nationalityId,
              dateOfBirth: g.full.dateOfBirth,
              documentType: g.full.documentType,
              documentNumber: g.full.documentNumber,
              address: g.full.address,
              reasonOfStay: g.full.reasonOfStay,
              stayFrom: g.full.stayFrom,
              stayTo: g.full.stayTo,
              visaNumber: g.full.visaNumber,
              note: g.full.note,
              scartation: g.full.scartation,
              checkInAt: g.full.checkInAt,
              checkOutAt: g.full.checkOutAt,
            }
          : null,
      };
    }
  );

  protected onBack(): void {
    void this.router.navigate(["/staff/auth/desktop/bills"]);
  }

  protected openRepairBill(repairBillId: string): void {
    void this.router.navigate(["/staff/auth/desktop/bills", repairBillId]);
  }

  protected onOpenReservation(): void {
    const id = this.bill()?.reservationId;
    if (id) {
      void this.router.navigate([
        "/staff/auth/desktop/reservations",
        id,
        "edit",
      ]);
    }
  }

  protected onOpenPdf(): void {
    const id = this.billId();
    if (!id || this.viewing()) {
      return;
    }
    this.viewing.set(true);
    this.billsApi
      .getPdf(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: blob => {
          this.viewing.set(false);
          const url = URL.createObjectURL(blob);
          window.open(url, "_blank", "noopener");
          setTimeout(() => URL.revokeObjectURL(url), 60_000);
        },
        error: () => {
          this.viewing.set(false);
          this.messages.add({
            severity: "error",
            summary: "Chyba",
            detail: "Stažení PDF se nepodařilo.",
          });
        },
      });
  }

  protected openRepairDialog(): void {
    const b = this.bill();
    if (!b || b.kind !== BillKind.Regular) {
      return;
    }
    this.repairReason.set("");
    this.repairLines.set(
      b.items.map(item => {
        const service = item.serviceId
          ? this.servicesStore.byId(item.serviceId)
          : null;
        return {
          id: item.id,
          serviceId: item.serviceId,
          serviceName: service?.name ?? "Bez služby",
          unitPrice: item.unitPrice,
          vatRatePercentage: item.vatRatePercentage,
          recapSingleQuantity: item.recapSingleQuantity,
          recapDayQuantity: item.recapDayQuantity,
          qty: 0,
        };
      })
    );
    this.repairPaymentType.set(b.payment.paymentType);
    this.repairOpen.set(true);
  }

  protected onRepairDialogVisibleChange(visible: boolean): void {
    this.repairOpen.set(visible);
  }

  protected updateRepairLineQty(itemId: string, qty: number | null): void {
    const next = Math.max(0, Math.trunc(qty ?? 0));
    this.repairLines.update(lines =>
      lines.map(line => {
        if (line.id !== itemId) {
          return line;
        }
        const capped = Math.min(this.repairLineMax(line), next);
        return { ...line, qty: capped };
      })
    );
  }

  protected repairLineMax(line: RepairLineDraft): number {
    return line.recapSingleQuantity * Math.max(1, line.recapDayQuantity);
  }

  protected submitRepair(): void {
    const b = this.bill();
    if (!b || this.repairSubmitting()) {
      return;
    }
    const reason = this.repairReason().trim();
    if (reason === "") {
      this.messages.add({
        severity: "warn",
        summary: "Opravná účtenka",
        detail: "Vyplňte důvod opravy.",
      });
      return;
    }
    const items = this.repairLines()
      .filter(line => line.qty > 0)
      .map(line => ({
        serviceId: line.serviceId,
        quantity: line.qty,
        unitPrice: line.unitPrice,
        vatRatePercentage: line.vatRatePercentage,
        recapSingleQuantity: line.qty,
        recapDayQuantity: 1,
      }));
    if (items.length === 0) {
      this.messages.add({
        severity: "warn",
        summary: "Opravná účtenka",
        detail: "Vyberte alespoň jednu položku s nenulovým množstvím.",
      });
      return;
    }
    this.repairSubmitting.set(true);
    this.billsApi
      .createRepair({
        originalBillId: b.id,
        paymentType: this.repairPaymentType(),
        reason,
        items,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: response => {
          this.repairSubmitting.set(false);
          this.repairOpen.set(false);
          this.messages.add({
            severity: "success",
            summary: "Opravná účtenka vystavena",
            detail: `Číslo ${response.number}`,
          });
          void this.router.navigate([
            "/staff/auth/desktop/bills",
            response.billId,
          ]);
        },
        error: (err: unknown) => {
          this.repairSubmitting.set(false);
          this.messages.add({
            severity: "error",
            summary: "Opravná účtenka",
            detail: extractRepairError(err),
          });
        },
      });
  }

  protected openGuest(guest: EnrichedGuest): void {
    this.editingGuestId.set(guest.id);
    this.guestDialogVisible.set(true);
  }

  protected onGuestDialogVisibleChange(visible: boolean): void {
    this.guestDialogVisible.set(visible);
    if (!visible) {
      this.editingGuestId.set(null);
    }
  }

  protected onGuestSaved(): void {
    this.reservationResource.reload();
    this.resource.reload();
    this.editingGuestId.set(null);
    this.guestDialogVisible.set(false);
  }

  protected kindLabel(kind: BillKind): string {
    switch (kind) {
      case BillKind.Regular:
        return "Běžná";
      case BillKind.Repair:
        return "Opravná";
    }
  }

  protected kindSeverity(kind: BillKind): "info" | "warn" {
    return kind === BillKind.Repair ? "warn" : "info";
  }

  protected paymentTypeLabel(type: PaymentType): string {
    switch (type) {
      case PaymentType.Cash:
        return "Hotově";
      case PaymentType.Card:
        return "Kartou";
    }
  }

  protected formatCzk(amount: number): string {
    return CZK.format(amount);
  }

  protected formatCzk2(amount: number): string {
    return CZK2.format(amount);
  }

  protected formatDate(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
    if (m) {
      return `${Number(m[3])}. ${Number(m[2])}. ${m[1]}`;
    }
    return iso;
  }

  protected formatIssued(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) {
      return iso;
    }
    return d.toLocaleString("cs-CZ", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  protected formatAddress(addr: BillAddress): string {
    const street = `${addr.street} ${addr.houseNumber}`.trim();
    const city = `${addr.zipCode} ${addr.city}`.trim();
    const country =
      this.nationalitiesStore.byId().get(addr.countryId)?.name ?? "";
    return [street, city, country].filter(s => s.length > 0).join(", ");
  }

  protected nationalityName(id: string): string {
    return this.nationalitiesStore.byId().get(id)?.name ?? "—";
  }

  protected documentTypeLabel(t: GuestDocumentType | null): string {
    if (t === null) {
      return "—";
    }
    return this.documentTypeNames.get(t) ?? "Neznámý";
  }

  protected paymentTypeName(type: PaymentType): string {
    return this.paymentTypeOptions.find(opt => opt.id === type)?.name ?? "—";
  }

  private toRow(item: BillItemView): ItemRow {
    const service = item.serviceId
      ? this.servicesStore.byId(item.serviceId)
      : null;
    const days = Math.max(1, item.recapDayQuantity);
    const lineTotal = days * item.recapSingleQuantity * item.unitPrice;
    return {
      id: item.id,
      serviceName: service?.name ?? "—",
      recapSingleQuantity: item.recapSingleQuantity,
      recapDayQuantity: days,
      unitPrice: item.unitPrice,
      vatRatePercentage: item.vatRatePercentage,
      lineTotal,
    };
  }
}

function extractRepairError(err: unknown): string {
  if (err !== null && typeof err === "object" && "error" in err) {
    const body = (err as { error?: unknown }).error;
    if (body !== null && typeof body === "object") {
      const detail =
        ("detail" in body &&
        typeof (body as { detail?: unknown }).detail === "string"
          ? (body as { detail: string }).detail
          : "") ||
        ("title" in body &&
        typeof (body as { title?: unknown }).title === "string"
          ? (body as { title: string }).title
          : "");
      if (detail) {
        return detail;
      }
    }
  }
  return "Opravnou účtenku se nepodařilo vystavit.";
}
