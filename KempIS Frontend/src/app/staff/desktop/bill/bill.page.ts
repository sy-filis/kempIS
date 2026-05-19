import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
  ViewEncapsulation,
} from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";

import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";
import { concatMap, defer, from, lastValueFrom, type Observable } from "rxjs";

import {
  BILL_STEPS,
  type BillStep,
  type DocType,
  type FeeCategoryId,
  type MealDay,
  type PreloadedGuest,
  STEP_LABELS,
  type Vehicle,
} from "./bill-data";
import { BillState } from "./bill-state";
import { StepAccessCards } from "./steps/step-access-cards";
import { Step1Period } from "./steps/step1-period";
import { Step2Vehicles } from "./steps/step2-vehicles";
import { Step3Cottages } from "./steps/step3-cottages";
import { Step4Meals } from "./steps/step4-meals";
import { Step5Invoices } from "./steps/step5-invoices";
import { Step6Other } from "./steps/step6-other";
import { Step7Recap } from "./steps/step7-recap";
import { Step8Payment } from "./steps/step8-payment";
import { ConnectTabletDialogComponent } from "./tablet-pairing/connect-tablet-dialog.component";
import { ReceptionPairingService } from "./tablet-pairing/reception-pairing.service";
import { equal as deepEqual } from "../../../../utils/deepEqual";
import { ApiClient } from "../../../core/api/api-client";
import { NationalitiesStore } from "../../../core/nationalities/nationalities.store";
import { PrinterServerApi } from "../../../core/printing/printer-server.api";
import { PrinterSettingsStore } from "../../../core/printing/printer-settings.store";
import type {
  BillSummaryDto,
  GuestSigningEntryDto,
} from "../../../core/reception-realtime/reception-event-types";
import { ServicesStore } from "../../../core/services/services.store";
import { isoToDate } from "../../../shared/date-iso";
import { BillsApi } from "../../api/bills.api";
import type {
  BillDetail,
  BillNewGuestRequest,
  CreateBillRequest,
} from "../../api/bills.types";
import {
  GuestDocumentType,
  type ReservationDetail,
  type ReservationDetailGuest,
  type ReservationDetailMeal,
  type ReservationMealAmount,
} from "../../api/reservations.types";
import type { RegistryGuest } from "../reservations/reservation-form/reservation-form-stub-data";
import { ServiceGroup } from "../system-settings/shared/service-groups";
import type { Nationality } from "../system-settings/shared/types";

const DEEP_EQUAL = { equal: deepEqual };

type StepState = "done" | "current" | "todo";

type StepView = {
  readonly step: BillStep;
  readonly index: number;
  readonly state: StepState;
};

type Mode = "create" | "edit";

@Component({
  selector: "kemp-is-bill",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    TagModule,
    ToastModule,
    Step1Period,
    Step2Vehicles,
    Step3Cottages,
    Step4Meals,
    Step5Invoices,
    Step6Other,
    Step7Recap,
    Step8Payment,
    StepAccessCards,
    ConnectTabletDialogComponent,
  ],
  templateUrl: "./bill.page.html",
  styleUrl: "./bill.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  providers: [BillState, MessageService, ConfirmationService],
})
export class BillPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly apiClient = inject(ApiClient);
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly servicesStore = inject(ServicesStore);
  private readonly billState = inject(BillState);
  private readonly billsApi = inject(BillsApi);
  private readonly messageService = inject(MessageService);
  private readonly confirmService = inject(ConfirmationService);
  private readonly pairing = inject(ReceptionPairingService);
  private readonly printerApi = inject(PrinterServerApi);
  private readonly printerStore = inject(PrinterSettingsStore);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly submitting = signal<boolean>(false);
  protected readonly tabletDialogVisible = signal<boolean>(false);

  protected readonly active = signal<string>(BILL_STEPS[0]?.id ?? "period");

  private readonly routeParams = toSignal(this.route.paramMap, {
    requireSync: true,
  });
  private readonly routeQuery = toSignal(this.route.queryParamMap, {
    requireSync: true,
  });

  protected readonly billId = computed<string | null>(() =>
    this.routeParams().get("id")
  );

  protected readonly pairingState = this.pairing.state;
  protected readonly paired = this.pairing.isPaired;
  protected readonly pairingButtonLabel = computed<string>(() =>
    this.paired() ? "Odpojit tablet" : "Spárovat tablet"
  );
  protected readonly pairingButtonSeverity = computed<"primary" | "secondary">(
    () => (this.paired() ? "secondary" : "primary")
  );
  protected readonly pairingTag = computed<{
    label: string;
    severity: "success" | "info" | "warn" | "danger" | "secondary";
  } | null>(() => {
    const s = this.pairingState();
    switch (s.kind) {
      case "idle":
        return null;
      case "issuing":
        return { label: "Vystavuji kód…", severity: "info" };
      case "waitingForTablet":
        return { label: "Čekám na tablet", severity: "info" };
      case "paired":
        return { label: "Tablet spárován", severity: "success" };
      case "displaced":
        return { label: "Převzal jiný desktop", severity: "warn" };
      case "error":
        return { label: "Chyba párování", severity: "danger" };
    }
  });

  protected readonly mode = computed<Mode>(() =>
    this.billId() ? "edit" : "create"
  );

  protected readonly linkedReservationId = computed<string | null>(() =>
    this.routeQuery().get("reservationId")
  );

  protected readonly duplicateFromBillId = computed<string | null>(() =>
    this.routeQuery().get("duplicateFromBillId")
  );

  protected readonly hasReservation = computed<boolean>(
    () => this.linkedReservationId() !== null || this.mode() === "edit"
  );

  protected readonly periodEditable = computed<boolean>(
    () => !this.hasReservation()
  );

  protected readonly titleText = computed<string>(() => {
    if (this.mode() === "edit") {
      const id = this.billId();
      return id ? `Účtenka #${id}` : "Účtenka";
    }
    return "Nová účtenka";
  });

  protected readonly visibleSteps = computed<readonly BillStep[]>(() => {
    const linked = this.hasReservation();
    return BILL_STEPS.filter(s => linked || !s.requiresReservation);
  });

  protected readonly activeIndex = computed<number>(() => {
    const id = this.active();
    const idx = this.visibleSteps().findIndex(s => s.id === id);
    return idx === -1 ? 0 : idx;
  });

  protected readonly steps = computed<readonly StepView[]>(() => {
    const a = this.activeIndex();
    return this.visibleSteps().map((step, index) => ({
      step,
      index,
      state: index < a ? "done" : index === a ? "current" : "todo",
    }));
  });

  private readonly reservationDetail = httpResource<ReservationDetail>(() => {
    const id = this.linkedReservationId();
    return id ? this.apiClient.url(`/reservations/${id}`) : undefined;
  });

  private readonly duplicateSource = httpResource<BillDetail>(() => {
    const id = this.duplicateFromBillId();
    return id
      ? this.apiClient.url(`/bills/${encodeURIComponent(id)}`)
      : undefined;
  });

  /** All reservation guests that aren't already attached to another bill.
   *  The `hasSignature` filter was previously here too, which hid guests
   *  who hadn't completed online check-in — making them invisible during
   *  bill creation from a fresh reservation. They are now preloaded
   *  regardless of signature; the tablet signing flow can still gather
   *  signatures for them. `{ equal: deepEqual }` so downstream consumers
   *  ignore identity-only changes when the filtered set is structurally
   *  unchanged. */
  private readonly eligibleReservationGuests = computed<
    readonly ReservationDetailGuest[]
  >(() => {
    if (!this.reservationDetail.hasValue()) {
      return [];
    }
    return this.reservationDetail.value().guests.filter(g => g.billId === null);
  }, DEEP_EQUAL);

  protected readonly linkedReservationSummary = computed<{
    readonly number: string;
    readonly maker: string;
  } | null>(() => {
    if (!this.reservationDetail.hasValue()) {
      return null;
    }
    const r = this.reservationDetail.value();
    const maker = [r.reservationMakerName, r.reservationMakerSurname]
      .filter(s => s.length > 0)
      .join(" ");
    return { number: r.number, maker };
  }, DEEP_EQUAL);

  constructor() {
    // The pairing service is hoisted to `DesktopHomePage`, so attach the
    // bill-context callbacks on mount (the previously-active page would
    // have overwritten them) and clear them on teardown to avoid stale
    // closures into this destroyed page's billState.
    this.attachPairing();
    this.destroyRef.onDestroy(() => {
      this.pairing.clearSession();
      this.pairing.detach();
    });

    if (
      this.billState.from() === null &&
      this.linkedReservationId() === null &&
      this.mode() === "create"
    ) {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      const tomorrow = new Date(
        today.getFullYear(),
        today.getMonth(),
        today.getDate() + 1
      );
      this.billState.from.set(today);
      this.billState.to.set(tomorrow);
    }
    effect(() => {
      if (!this.reservationDetail.hasValue()) {
        return;
      }
      if (this.billState.from() !== null) {
        return;
      }
      const r = this.reservationDetail.value();
      const from = isoToDate(r.from);
      const to = isoToDate(r.to);
      if (from) {
        this.billState.from.set(from);
      }
      if (to) {
        this.billState.to.set(to);
      }
    });

    effect(() => {
      const guests = this.eligibleReservationGuests();
      const byId = this.nationalitiesStore.byId();
      if (guests.length === 0 || byId.size === 0) {
        return;
      }
      if (this.billState.preloadedGuests().length > 0) {
        return;
      }
      this.billState.preloadedGuests.set(
        guests.map(g => mapReservationGuestToPreloaded(g, byId))
      );
    });

    effect(() => {
      if (!this.duplicateSource.hasValue()) {
        return;
      }
      if (this.servicesStore.active().length === 0) {
        return;
      }
      if (this.billState.duplicateSeeded()) {
        return;
      }
      this.seedFromDuplicate(this.duplicateSource.value());
      this.billState.duplicateSeeded.set(true);
    });

    effect(() => {
      if (!this.reservationDetail.hasValue()) {
        return;
      }
      const assigned = this.reservationDetail
        .value()
        .spotItems.filter(i => i.spotId !== null);
      this.billState.reservationSpotItems.set(
        assigned.map(i => ({
          itemId: i.id,
          spotId: i.spotId as string,
          billId: i.billId,
          hasGivenKey: i.hasGivenKey,
          hasReturnedKeys: i.hasReturnedKeys,
        }))
      );
      if (this.billState.selectedSpotItemIds().size === 0) {
        this.billState.selectedSpotItemIds.set(
          new Set(assigned.filter(i => i.billId === null).map(i => i.id))
        );
      }
    });

    effect(() => {
      if (!this.reservationDetail.hasValue()) {
        return;
      }
      if (this.billState.meals().length > 0) {
        return;
      }
      const days = this.reservationDetail
        .value()
        .meals.map(mapReservationMealToDay);
      this.billState.meals.set(days);
    });

    effect(() => {
      if (!this.reservationDetail.hasValue()) {
        return;
      }
      this.billState.reservationInvoices.set(
        this.reservationDetail.value().invoices
      );
    });

    effect(() => {
      if (!this.reservationDetail.hasValue()) {
        return;
      }
      const services = this.servicesStore.active();
      if (services.length === 0) {
        return;
      }
      if (this.billState.vehiclesSeeded()) {
        return;
      }

      const vehicleGroupIds = new Set(
        this.servicesStore.byGroup(ServiceGroup.Vehicles).map(s => s.id)
      );
      const caravanGroupIds = new Set(
        this.servicesStore.byGroup(ServiceGroup.MotorHomes).map(s => s.id)
      );
      const nights = Math.max(1, this.billState.nights());

      const vehicles: Vehicle[] = [];
      const caravans: Vehicle[] = [];
      const unassigned: Vehicle[] = [];

      let n = 0;
      for (const v of this.reservationDetail.value().vehicles) {
        if (v.billId !== null) {
          continue;
        }
        n += 1;
        const localId = `R${n}`;
        if (v.serviceId === null) {
          unassigned.push({
            id: localId,
            persistentId: v.id,
            plate: v.registrationNumber,
            type: "",
            serviceId: null,
            nights,
            ratePerNight: 0,
          });
          continue;
        }
        const svc = services.find(s => s.id === v.serviceId);
        const row: Vehicle = {
          id: localId,
          persistentId: v.id,
          plate: v.registrationNumber,
          type: svc?.name ?? "",
          serviceId: v.serviceId,
          nights,
          ratePerNight: svc?.basePrice ?? 0,
        };
        if (vehicleGroupIds.has(v.serviceId)) {
          vehicles.push(row);
        } else if (caravanGroupIds.has(v.serviceId)) {
          caravans.push(row);
        } else {
          unassigned.push(row);
        }
      }

      this.billState.vehicles.set(vehicles);
      this.billState.caravans.set(caravans);
      this.billState.unassignedVehicles.set(unassigned);
      this.billState.vehiclesSeeded.set(true);
    });

    effect(() => {
      if (!this.pairing.isPaired()) {
        return;
      }
      this.pushSessionFromState();
    });
  }

  protected onPairingButtonClick(): void {
    // Mirrors the settings page: paired → disconnect, otherwise open the
    // pair-code dialog. Sink/rebuild are attached in the constructor.
    if (this.paired()) {
      this.pairing.disconnect();
      return;
    }
    this.tabletDialogVisible.set(true);
  }

  private attachPairing(): void {
    this.pairing.attach(
      {
        resolvePersistedGuestId: (cid: string): string | null => {
          const preloaded = this.billState
            .preloadedGuests()
            .find(g => g.id === cid);
          return preloaded ? preloaded.id : null;
        },
        bufferDraftSignature: (cid: string, png: string): void => {
          this.billState.bufferRegistrySignature(cid, png);
        },
      },
      () => this.pushSessionFromState()
    );
  }

  private pushSessionFromState(): void {
    const bill = this.buildBillDto();
    if (!bill) {
      return;
    }
    const guests = this.buildGuestsDto();
    this.pairing.pushSession({ bill, guests });
  }

  private seedFromDuplicate(source: BillDetail): void {
    const nationalitiesById = this.nationalitiesStore.byId();
    const alpha2 = (id: string): string =>
      nationalitiesById.get(id)?.alpha2 ?? "CZ";

    this.billState.payer.set({
      name: source.payer.name,
      surname: source.payer.surname,
      street: source.payer.address.street,
      houseNumber: source.payer.address.houseNumber,
      city: source.payer.address.city,
      zipCode: source.payer.address.zipCode,
      countryCode: alpha2(source.payer.address.countryId),
    });
    this.billState.payerSourceId.set("__custom__");

    if (source.legalEntity) {
      const le = source.legalEntity;
      this.billState.legalEntity.set({
        name: le.name,
        cin: le.cin,
        tin: le.tin,
        street: le.address.street,
        houseNumber: le.address.houseNumber,
        city: le.address.city,
        zipCode: le.address.zipCode,
        countryCode: alpha2(le.address.countryId),
      });
    } else {
      this.billState.legalEntity.set(null);
    }

    this.billState.paymentType.set(source.payment.paymentType);
    this.billState.languageId.set(source.languageId);

    const vehicleGroupIds = new Set(
      this.servicesStore.byGroup(ServiceGroup.Vehicles).map(s => s.id)
    );
    const caravanGroupIds = new Set(
      this.servicesStore.byGroup(ServiceGroup.MotorHomes).map(s => s.id)
    );
    const tentGroupIds = new Set(
      this.servicesStore.byGroup(ServiceGroup.Tents).map(s => s.id)
    );

    const vehicles: Vehicle[] = [];
    const caravans: Vehicle[] = [];
    const tentQtys = new Map<string, number>();
    const serviceQtys = new Map<string, number>();
    const recapOverrides = new Map<string, { qty: number; days: number }>();

    let localId = 0;
    for (const item of source.items) {
      if (item.serviceId === null) {
        continue;
      }
      const svc = this.servicesStore.byId(item.serviceId);
      const recap = {
        qty: item.recapSingleQuantity,
        days: item.recapDayQuantity,
      };

      if (vehicleGroupIds.has(item.serviceId)) {
        for (let i = 0; i < item.quantity; i += 1) {
          localId += 1;
          vehicles.push({
            id: `D${localId}`,
            persistentId: null,
            plate: "",
            type: svc?.name ?? "",
            serviceId: item.serviceId,
            nights: Math.max(1, this.billState.nights()),
            ratePerNight: item.unitPrice,
          });
        }
        recapOverrides.set(`veh-group-${item.serviceId}`, recap);
        continue;
      }
      if (caravanGroupIds.has(item.serviceId)) {
        for (let i = 0; i < item.quantity; i += 1) {
          localId += 1;
          caravans.push({
            id: `D${localId}`,
            persistentId: null,
            plate: "",
            type: svc?.name ?? "",
            serviceId: item.serviceId,
            nights: Math.max(1, this.billState.nights()),
            ratePerNight: item.unitPrice,
          });
        }
        recapOverrides.set(`car-group-${item.serviceId}`, recap);
        continue;
      }
      if (tentGroupIds.has(item.serviceId)) {
        tentQtys.set(item.serviceId, item.quantity);
        recapOverrides.set(`tent-${item.serviceId}`, recap);
        continue;
      }
      serviceQtys.set(item.serviceId, item.quantity);
      recapOverrides.set(`svc-${item.serviceId}`, recap);
    }

    this.billState.vehicles.set(vehicles);
    this.billState.caravans.set(caravans);
    if (tentQtys.size > 0) {
      this.billState.tentQtys.update(m => {
        const next = new Map(m);
        for (const [k, v] of tentQtys) {
          next.set(k, v);
        }
        return next;
      });
    }
    if (serviceQtys.size > 0) {
      this.billState.serviceQtys.update(m => {
        const next = new Map(m);
        for (const [k, v] of serviceQtys) {
          next.set(k, v);
        }
        return next;
      });
    }
    if (recapOverrides.size > 0) {
      this.billState.recapOverrides.update(m => {
        const next = new Map(m);
        for (const [k, v] of recapOverrides) {
          next.set(k, v);
        }
        return next;
      });
    }

    const registryGuests: RegistryGuest[] = [];
    const feePayingIds = new Set<string>();
    for (const g of source.guests) {
      if (
        g.nationalityId === undefined ||
        g.dateOfBirth === undefined ||
        g.address === undefined
      ) {
        continue;
      }
      const localId = crypto.randomUUID() as string;
      registryGuests.push({
        id: localId,
        firstName: g.firstName,
        lastName: g.lastName,
        nationalityId: g.nationalityId,
        birth: formatBirthFromIso(g.dateOfBirth),
        documentType:
          g.documentType === undefined
            ? null
            : (g.documentType as RegistryGuest["documentType"]),
        documentNumber: g.documentNumber ?? null,
        visaNumber: g.visaNumber ?? null,
        address: {
          street: g.address.street,
          houseNumber: g.address.houseNumber,
          zipCode: g.address.zipCode,
          city: g.address.city,
          countryCode: alpha2(g.address.countryId),
        },
        note: g.note ?? null,
      });
      if (g.paysRecreationFee === true) {
        feePayingIds.add(localId);
      }
    }
    if (registryGuests.length > 0) {
      this.billState.registryGuests.set(registryGuests);
      this.billState.registryFeePayingIds.set(feePayingIds);
    }

    if (source.accessCards && source.accessCards.length > 0) {
      const fallback = dateToIso(defaultCardValidUntil(this.billState.to()));
      this.billState.accessCards.set(
        source.accessCards.map(c => ({
          id: crypto.randomUUID() as string,
          uid: c.uid,
          deposit: c.deposit,
          validUntil: c.validUntil ?? fallback,
          note: c.note ?? "",
        }))
      );
    }
  }

  private buildBillDto(): BillSummaryDto | null {
    const from = this.billState.from();
    const to = this.billState.to();
    if (!from || !to) {
      return null;
    }
    const payer = this.billState.payer();
    const rows = this.billState.finalRecapRows();
    const id = this.billId();
    return {
      billId: id ?? "draft",
      number: id ?? "draft",
      payerDisplayName: `${payer.name} ${payer.surname}`.trim(),
      checkInAt: dateToIso(from),
      checkOutAt: dateToIso(to),
      total: this.billState.grandTotal(),
      currency: "CZK",
      lines: rows.map(r => ({
        label: r.service,
        quantity: r.qty * Math.max(1, r.days),
        unitPrice: r.price,
        total: r.qty * Math.max(1, r.days) * r.price,
      })),
    };
  }

  private buildGuestsDto(): readonly GuestSigningEntryDto[] {
    const out: GuestSigningEntryDto[] = [];
    const nationalitiesById = this.nationalitiesStore.byId();
    for (const g of this.billState.preloadedGuests()) {
      if (!g.checked) {
        continue;
      }
      out.push({
        clientGuestId: g.id,
        fullName: `${g.firstName} ${g.surname}`.trim(),
        nationality: g.citizenship,
        isCzech: g.citizenship === "CZ",
        hasSignature: true,
        hasEDokladyResult: false,
      });
    }
    const signatures = this.billState.registryGuestSignatures();
    for (const g of this.billState.registryGuests()) {
      const alpha2 = nationalitiesById.get(g.nationalityId)?.alpha2 ?? "";
      out.push({
        clientGuestId: g.id,
        fullName: `${g.firstName} ${g.lastName}`.trim(),
        nationality: alpha2,
        isCzech: alpha2 === "CZ",
        hasSignature: signatures.has(g.id),
        hasEDokladyResult: false,
      });
    }
    return out;
  }

  protected readonly currentTotals = computed(() => ({
    total: this.billState.grandTotal(),
    label: STEP_LABELS[this.active()] ?? "Pokračovat",
  }));

  protected readonly isFirst = computed(() => this.activeIndex() === 0);
  protected readonly isLast = computed(
    () => this.activeIndex() === this.visibleSteps().length - 1
  );

  protected setStep(index: number): void {
    const step = this.visibleSteps()[index];
    if (step) {
      this.active.set(step.id);
    }
  }

  protected onPrev(): void {
    const idx = this.activeIndex();
    const prev = this.visibleSteps()[idx - 1];
    if (prev) {
      this.active.set(prev.id);
    }
  }

  protected onNext(): void {
    const idx = this.activeIndex();
    const next = this.visibleSteps()[idx + 1];
    if (next) {
      this.active.set(next.id);
    }
  }

  protected onCancel(): void {
    void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
  }

  protected onSubmit(): void {
    if (this.submitting()) {
      return;
    }
    const request = this.buildCreateRequest();
    if (!request) {
      return;
    }
    this.submitting.set(true);
    this.billsApi.create(request).subscribe({
      next: response => {
        this.submitting.set(false);
        this.messageService.add({
          severity: "success",
          summary: "Účet vystaven",
          detail: "Účet byl úspěšně uložen.",
        });
        this.printAfterSave(response.id);
        this.maybePromptCheckIn();
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.messageService.add({
          severity: "error",
          summary: "Účet se nepodařilo uložit",
          detail: extractErrorMessage(err),
        });
      },
    });
  }

  private printAfterSave(billId: string): void {
    const server = this.printerStore.serverUrl();

    if (this.billState.printBill()) {
      const billPrinter = this.printerStore.defaultFor("bill")();
      const billCopies = Math.max(1, this.billState.printBillCopies());
      if (billPrinter === null || server === "") {
        this.messageService.add({
          severity: "warn",
          summary: "Tisk",
          detail: "Účet: není nastavena tiskárna.",
        });
      } else {
        this.dispatchPrint(
          server,
          billPrinter,
          billCopies,
          () => this.billsApi.getPdf(billId),
          "Účet"
        );
      }
    }

    const tentTotal = this.billState.tents().reduce((sum, t) => sum + t.qty, 0);
    if (tentTotal <= 0 || !this.billState.printTentStickers()) {
      return;
    }
    const stickerPrinter = this.printerStore.defaultFor("tent-sticker")();
    if (stickerPrinter === null || server === "") {
      this.messageService.add({
        severity: "warn",
        summary: "Tisk",
        detail: "Nálepky: není nastavena tiskárna.",
      });
      return;
    }
    const stickerCopies = Math.max(1, this.billState.printTentStickerCopies());
    this.dispatchPrint(
      server,
      stickerPrinter,
      stickerCopies,
      () => this.billsApi.getSticker(billId),
      "Nálepky"
    );
  }

  private dispatchPrint(
    server: string,
    printer: string,
    copies: number,
    fetchPdf: () => Observable<Blob>,
    label: string
  ): void {
    fetchPdf()
      .pipe(
        concatMap(blob =>
          defer(() => from(this.printNTimes(server, printer, blob, copies)))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: "info",
            summary: "Tisk",
            detail:
              copies === 1
                ? `${label}: odesláno na tiskárnu.`
                : `${label}: odesláno na tiskárnu (${copies}×).`,
          });
        },
        error: () => {
          this.messageService.add({
            severity: "error",
            summary: "Tisk",
            detail: `${label}: tisk se nepodařil.`,
          });
        },
      });
  }

  private async printNTimes(
    server: string,
    printer: string,
    blob: Blob,
    copies: number
  ): Promise<void> {
    for (let i = 0; i < copies; i++) {
      await lastValueFrom(this.printerApi.printPdf(server, printer, blob));
    }
  }

  private maybePromptCheckIn(): void {
    const reservationId = this.linkedReservationId();
    if (!reservationId) {
      void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
      return;
    }
    const items = this.billState.reservationSpotItems();
    if (items.length === 0) {
      void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
      return;
    }
    const selected = this.billState.selectedSpotItemIds();
    const allCovered = items.every(
      i => i.billId !== null || selected.has(i.itemId)
    );
    if (!allCovered) {
      void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
      return;
    }
    this.confirmService.confirm({
      header: "Provést check-in?",
      message:
        "Tímto účtem byly vyúčtovány všechny chatky z rezervace. Chcete rezervaci označit jako přihlášenou?",
      icon: "pi pi-id-card",
      acceptLabel: "Provést check-in",
      rejectLabel: "Ne",
      accept: () => this.performCheckIn(reservationId),
      reject: () => {
        void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
      },
    });
  }

  private performCheckIn(reservationId: string): void {
    this.apiClient
      .post<void>(
        `/reservations/${encodeURIComponent(reservationId)}/check-in`,
        {}
      )
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: "success",
            summary: "Rezervace přihlášena",
            detail: "Rezervace byla označena jako přihlášená.",
          });
          void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
        },
        error: (err: unknown) => {
          this.messageService.add({
            severity: "error",
            summary: "Check-in se nezdařil",
            detail: extractErrorMessage(err),
          });
          void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
        },
      });
  }

  private buildCreateRequest(): CreateBillRequest | null {
    const from = this.billState.from();
    const to = this.billState.to();
    if (!from || !to) {
      this.messageService.add({
        severity: "warn",
        summary: "Chybí termín",
        detail: "Vyplňte termín pobytu na kroku Termín.",
      });
      return null;
    }
    const languageId = this.billState.languageId();
    if (!languageId) {
      this.messageService.add({
        severity: "warn",
        summary: "Chybí jazyk",
        detail: "Vyberte jazyk účtenky v kroku Platba.",
      });
      return null;
    }
    const payer = this.billState.payer();
    const nationalities = this.nationalitiesStore;
    if (payer.name.trim() === "" || payer.surname.trim() === "") {
      this.messageService.add({
        severity: "warn",
        summary: "Chybí plátce",
        detail: "Vyplňte jméno a příjmení plátce v kroku Platba.",
      });
      return null;
    }
    const countryId = nationalityIdFromAlpha2(
      payer.countryCode,
      nationalities.all()
    );
    if (!countryId) {
      this.messageService.add({
        severity: "warn",
        summary: "Chybí stát plátce",
        detail: "Vyberte stát v adrese plátce.",
      });
      return null;
    }

    const existingGuests = this.billState
      .preloadedGuests()
      .filter(g => g.checked)
      .map(g => ({ guestId: g.id, paysRecreationFee: g.paysFee }));

    const newGuestsResult = this.buildNewGuests();
    if (!newGuestsResult.ok) {
      this.messageService.add({
        severity: "warn",
        summary: "Neúplný host",
        detail: newGuestsResult.error,
      });
      return null;
    }

    // VAT rate is omitted; backend re-derives it from the catalogue
    // service and discards whatever the FE sends.
    const items = this.billState.finalRecapRows().map(r => ({
      serviceId: parseRecapServiceId(r.id),
      quantity: r.qty,
      unitPrice: r.price,
      recapSingleQuantity: r.qty,
      recapDayQuantity: r.days,
    }));

    if (items.length === 0) {
      this.messageService.add({
        severity: "warn",
        summary: "Účet nemá žádné položky",
        detail: "Přidejte alespoň jednu službu před uzavřením účtu.",
      });
      return null;
    }

    const accessCards = this.billState.accessCards().map(c => ({
      uid: c.uid,
      deposit: c.deposit,
      validUntil: c.validUntil,
      note: c.note.trim() === "" ? null : c.note.trim(),
    }));

    let legalEntity: CreateBillRequest["legalEntity"] = null;
    const le = this.billState.legalEntity();
    if (le !== null) {
      const leCountryId = nationalityIdFromAlpha2(
        le.countryCode,
        nationalities.all()
      );
      if (!leCountryId) {
        this.messageService.add({
          severity: "warn",
          summary: "Chybí stát firmy",
          detail: "Vyberte stát v adrese firmy.",
        });
        return null;
      }
      if (le.name.trim() === "" || le.cin.trim() === "") {
        this.messageService.add({
          severity: "warn",
          summary: "Neúplná firma",
          detail: "Vyplňte název a IČO právnické osoby.",
        });
        return null;
      }
      legalEntity = {
        name: le.name.trim(),
        cin: le.cin.trim(),
        tin: le.tin.trim(),
        address: {
          countryId: leCountryId,
          city: le.city.trim(),
          zipCode: le.zipCode.trim(),
          street: le.street.trim(),
          houseNumber: le.houseNumber.trim(),
        },
      };
    }

    return {
      reservationId: this.linkedReservationId(),
      checkInAt: dateToIso(from),
      checkOutAt: dateToIso(to),
      payer: {
        name: payer.name.trim(),
        surname: payer.surname.trim(),
        address: {
          countryId,
          city: payer.city.trim(),
          zipCode: payer.zipCode.trim(),
          street: payer.street.trim(),
          houseNumber: payer.houseNumber.trim(),
        },
      },
      legalEntity,
      paymentType: this.billState.paymentType(),
      languageId,
      items,
      linkedInvoiceIds: Array.from(this.billState.linkedInvoiceIds()),
      existingGuests,
      newGuests: newGuestsResult.guests,
      reservationSpotItemIds: Array.from(this.billState.selectedSpotItemIds()),
      accessCards,
    };
  }

  private buildNewGuests():
    | { ok: true; guests: readonly BillNewGuestRequest[] }
    | { ok: false; error: string } {
    const list = this.billState.registryGuests();
    const feePaying = this.billState.registryFeePayingIds();
    const signatures = this.billState.registryGuestSignatures();
    const nationalities = this.nationalitiesStore.all();
    const from = this.billState.from();
    const to = this.billState.to();
    const guests: BillNewGuestRequest[] = [];
    for (const g of list) {
      const countryId = nationalityIdFromAlpha2(
        g.address.countryCode,
        nationalities
      );
      if (!countryId) {
        return {
          ok: false,
          error: `Host ${g.firstName} ${g.lastName}: chybí stát adresy.`,
        };
      }
      const dateOfBirth = isoFromCzechDate(g.birth);
      if (!dateOfBirth) {
        return {
          ok: false,
          error: `Host ${g.firstName} ${g.lastName}: chybí datum narození.`,
        };
      }
      guests.push({
        firstName: g.firstName,
        lastName: g.lastName,
        nationalityId: g.nationalityId,
        dateOfBirth,
        documentType: g.documentType ?? 0,
        // Backend requires non-empty DocumentNumber; fall back to a
        // dash so quick-add rows (typical for children) validate.
        documentNumber:
          g.documentNumber && g.documentNumber.trim() !== ""
            ? g.documentNumber
            : "—",
        address: {
          countryId,
          city: g.address.city,
          zipCode: g.address.zipCode,
          street: g.address.street,
          houseNumber: g.address.houseNumber,
        },
        reasonOfStay: "Rekreace",
        stayFrom: from ? dateToIso(from) : "",
        stayTo: to ? dateToIso(to) : "",
        visaNumber: g.visaNumber,
        note: g.note,
        paysRecreationFee: feePaying.has(g.id),
        signaturePngBase64: signatures.get(g.id) ?? null,
      });
    }
    return { ok: true, guests };
  }

  protected formatNumber(value: number): string {
    return value.toLocaleString("cs-CZ");
  }
}

function mapReservationGuestToPreloaded(
  guest: ReservationDetailGuest,
  nationalitiesById: ReadonlyMap<string, Nationality>
): PreloadedGuest {
  const age = ageFromIso(guest.dateOfBirth);
  const adult = age >= 18;
  return {
    id: guest.id,
    firstName: guest.firstName,
    surname: guest.lastName,
    birth: formatBirthFromIso(guest.dateOfBirth),
    street: guest.address.street,
    houseNumber: guest.address.houseNumber,
    postalCode: guest.address.zipCode,
    city: guest.address.city,
    country: nationalitiesById.get(guest.address.countryId)?.alpha2 ?? "",
    citizenship: nationalitiesById.get(guest.nationalityId)?.alpha2 ?? "",
    docType: mapDocumentType(guest.documentType),
    docNumber: guest.documentNumber ?? "",
    fee: feeCategoryForAge(age),
    // Signed guests are pre-included by default; unsigned guests are
    // surfaced but left unchecked so the operator opts them in deliberately.
    checked: guest.hasSignature,
    paysFee: adult,
    payer: false,
  };
}

function mapDocumentType(t: GuestDocumentType | null): DocType {
  if (t === GuestDocumentType.IdCard) {
    return "op";
  }
  if (t === GuestDocumentType.Passport) {
    return "passport";
  }
  return "other";
}

function feeCategoryForAge(age: number): FeeCategoryId {
  if (age < 6) {
    return "child0_6";
  }
  if (age < 18) {
    return "child6_18";
  }
  return "adult";
}

function ageFromIso(iso: string, today: Date = new Date()): number {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!m) {
    return 0;
  }
  const birth = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  let age = today.getFullYear() - birth.getFullYear();
  const monthDiff = today.getMonth() - birth.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
    age -= 1;
  }
  return Math.max(0, age);
}

function formatBirthFromIso(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!m) {
    return iso;
  }
  return `${Number(m[3])}. ${Number(m[2])}. ${Number(m[1])}`;
}

const DOW_LABELS_CZ = ["Ne", "Po", "Út", "St", "Čt", "Pá", "So"] as const;

function mapReservationMealToDay(meal: ReservationDetailMeal): MealDay {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(meal.date);
  const date = m
    ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))
    : new Date();
  const dow = DOW_LABELS_CZ[date.getDay()] ?? "";
  return {
    date: `${dow} ${date.getDate()}. ${date.getMonth() + 1}.`,
    dow,
    day: date.getDate(),
    b: sumMealAmount(meal.breakfast),
    l: sumMealAmount(meal.lunch),
    lp: sumMealAmount(meal.lunchPackage),
    d: sumMealAmount(meal.dinner),
  };
}

function sumMealAmount(a: ReservationMealAmount): number {
  return (
    a.normal +
    a.glutenFree +
    a.lactoseFree +
    a.vegetarian +
    a.glutenFreeLactoseFree +
    a.glutenFreeVegetarian +
    a.lactoseFreeVegetarian +
    a.glutenFreeLactoseFreeVegetarian
  );
}

function dateToIso(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function defaultCardValidUntil(checkout: Date | null): Date {
  if (checkout) {
    const d = new Date(checkout);
    d.setHours(0, 0, 0, 0);
    return d;
  }
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() + 7);
  return d;
}

function isoFromCzechDate(s: string): string | null {
  const m = /^(\d{1,2})\.\s*(\d{1,2})\.\s*(\d{4})$/.exec(s);
  if (!m) {
    return null;
  }
  const dd = String(Number(m[1])).padStart(2, "0");
  const mm = String(Number(m[2])).padStart(2, "0");
  return `${m[3]}-${mm}-${dd}`;
}

function nationalityIdFromAlpha2(
  alpha2: string,
  nationalities: readonly Nationality[]
): string | null {
  if (alpha2 === "") {
    return null;
  }
  return nationalities.find(n => n.alpha2 === alpha2)?.id ?? null;
}

/** Vehicle / caravan / tent / recreation-fee rows have synthetic ids;
 *  buckets with no catalogue service use a `_`-prefixed tail, send those
 *  as ad-hoc items with `serviceId: null`. */
function parseRecapServiceId(rowId: string): string | null {
  if (rowId.startsWith("svc-")) {
    return rowId.slice(4);
  }
  if (rowId.startsWith("meal-")) {
    return rowId.slice(5);
  }
  for (const prefix of [
    "spot-group-",
    "veh-group-",
    "car-group-",
    "tent-",
  ] as const) {
    if (rowId.startsWith(prefix)) {
      const tail = rowId.slice(prefix.length);
      return tail.startsWith("_") ? null : tail;
    }
  }
  return null;
}

function extractErrorMessage(err: unknown): string {
  if (err === null || err === undefined) {
    return "Neznámá chyba.";
  }
  if (typeof err === "object" && "message" in err) {
    const m = (err as { message?: unknown }).message;
    if (typeof m === "string") {
      return m;
    }
  }
  return "Volání API selhalo.";
}
