import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  ViewEncapsulation,
} from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";

import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { MessageModule } from "primeng/message";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";
import { catchError, forkJoin, type Observable, of } from "rxjs";

import type { Vehicle } from "./reservation-form-stub-data";
import { type SpotRow, StepCottages } from "./steps/step-cottages";
import { StepDoklady } from "./steps/step-doklady";
import { StepMeals } from "./steps/step-meals";
import { StepPeriod } from "./steps/step-period";
import { StepServices } from "./steps/step-services";
import { StepVehicles } from "./steps/step-vehicles";
import { ApiClient } from "../../../../core/api/api-client";
import { AuthService } from "../../../../core/auth/auth.service";
import { Roles } from "../../../../core/auth/roles";
import { ServicesStore } from "../../../../core/services/services.store";
import { dateToIso, isoToDate } from "../../../../shared/date-iso";
import type { GroupReservationDetail } from "../../../api/group-reservations.types";
import { type GuestRequest, GuestsApi } from "../../../api/guests.api";
import { InvoiceStatus } from "../../../api/invoices.types";
import {
  type ReservationDetail,
  type ReservationDetailBill,
  type ReservationDetailGuest,
  type ReservationDetailInvoice,
  type ReservationDetailMeal,
  type ReservationRequest,
  type ReservationServiceRequest,
  ReservationState,
  type ReservationVehicleRequest,
} from "../../../api/reservations.types";
import type { Spot, SpotGroup } from "../../../api/spots.types";
import { ServiceGroup } from "../../system-settings/shared/service-groups";

const EMAIL_RE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
const PHONE_RE = /^\+?\d{9,15}$/;

function nightsBetweenIso(fromIso: string, toIso: string): number {
  const ms =
    Date.parse(`${toIso}T00:00:00Z`) - Date.parse(`${fromIso}T00:00:00Z`);
  if (!Number.isFinite(ms)) {
    return 0;
  }
  return Math.max(0, Math.round(ms / 86_400_000));
}

type Mode = "create" | "edit";

type StepKey =
  | "period"
  | "vehicles"
  | "cottages"
  | "meals"
  | "doklady"
  | "services";

type StepDef = {
  readonly key: StepKey;
  readonly label: string;
  readonly stub: boolean;
  readonly editOnly: boolean;
};

const STEPS: readonly StepDef[] = [
  { key: "period", label: "Termín a hosté", stub: false, editOnly: false },
  { key: "vehicles", label: "Vozidla a stany", stub: false, editOnly: false },
  { key: "cottages", label: "Chatky", stub: true, editOnly: false },
  { key: "meals", label: "Stravování", stub: true, editOnly: true },
  { key: "services", label: "Ostatní služby", stub: true, editOnly: false },
  { key: "doklady", label: "Doklady", stub: false, editOnly: true },
];

const CANCELLABLE_STATES: readonly ReservationState[] = [
  ReservationState.Created,
  ReservationState.Confirmed,
];

@Component({
  selector: "kemp-is-reservation-form",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    MessageModule,
    TagModule,
    ToastModule,
    StepPeriod,
    StepCottages,
    StepDoklady,
    StepMeals,
    StepServices,
    StepVehicles,
  ],
  templateUrl: "./reservation-form.page.html",
  styleUrl: "./reservation-form.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  providers: [ConfirmationService, MessageService],
})
export class ReservationFormPage {
  private readonly apiClient = inject(ApiClient);
  private readonly servicesStore = inject(ServicesStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly guestsApi = inject(GuestsApi);

  private readonly guestsToCopy = signal<readonly ReservationDetailGuest[]>([]);

  protected readonly canCreateBill = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Receptionist)
  );

  protected onCreateBill(): void {
    const id = this.reservationId();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/bill/new"], {
      queryParams: { reservationId: id },
    });
  }

  protected onDuplicate(): void {
    const id = this.reservationId();
    if (!id) {
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/reservations/new"], {
      queryParams: { fromId: id },
    });
  }

  // Anyone with the magic link can view and check in the reservation,
  // so the button only surfaces once the detail has loaded.
  protected onCopyGuestLink(): void {
    if (!this.detail.hasValue()) {
      return;
    }
    const d = this.detail.value();
    const url = `${window.location.origin}/public/reservations/${d.id}?secret=${encodeURIComponent(d.secret)}`;
    void this.copyOrShowLink(url);
  }

  // `navigator.clipboard` is undefined in non-secure contexts and older
  // browsers, and `writeText` can also reject under permission policies. Both
  // failure modes fall back to surfacing the URL in a toast for manual copy.
  private async copyOrShowLink(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.messageService.add({
        severity: "success",
        summary: "Odkaz pro hosta",
        detail: "Odkaz byl zkopírován do schránky.",
      });
    } catch {
      this.messageService.add({
        severity: "warn",
        summary: "Odkaz pro hosta",
        detail: url,
      });
    }
  }
  private readonly messageService = inject(MessageService);
  private readonly confirmService = inject(ConfirmationService);

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly validationErrors = signal<readonly string[]>([]);

  private readonly routeParams = toSignal(this.route.paramMap, {
    requireSync: true,
  });

  protected readonly reservationId = computed<string | null>(() =>
    this.routeParams().get("id")
  );
  protected readonly mode = computed<Mode>(() =>
    this.reservationId() ? "edit" : "create"
  );

  protected readonly detail = httpResource<ReservationDetail>(() => {
    const id = this.reservationId();
    return id ? this.apiClient.url(`/reservations/${id}`) : undefined;
  });

  private readonly sourceDetail = httpResource<ReservationDetail>(() => {
    if (this.mode() !== "create" || !this.copyFromId) {
      return undefined;
    }
    return this.apiClient.url(`/reservations/${this.copyFromId}`);
  });

  protected readonly visibleSteps = computed<readonly StepDef[]>(() => {
    const m = this.mode();
    return STEPS.filter(s => m === "edit" || !s.editOnly);
  });

  protected readonly activeStepIndex = signal<number>(0);

  protected readonly fromDate = signal<Date | null>(null);
  protected readonly toDate = signal<Date | null>(null);
  // Clamped to >= 1 so empty states don't render zero-night line items.
  protected readonly nights = computed<number>(() => {
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t) {
      return 1;
    }
    const ms = t.getTime() - f.getTime();
    return Math.max(1, Math.round(ms / 86_400_000));
  });
  protected readonly name = signal<string>("");
  protected readonly surname = signal<string>("");
  protected readonly phone = signal<string>("");
  protected readonly email = signal<string>("");

  private readonly spotsResource = httpResource<readonly Spot[]>(() =>
    this.apiClient.url("/spots")
  );
  private readonly spotGroupsResource = httpResource<readonly SpotGroup[]>(() =>
    this.apiClient.url("/spot-groups")
  );

  protected readonly spots = computed<readonly Spot[]>(
    () => this.spotsResource.value() ?? []
  );
  protected readonly spotGroups = computed<readonly SpotGroup[]>(
    () => this.spotGroupsResource.value() ?? []
  );

  protected readonly spotRows = signal<readonly SpotRow[]>([]);

  protected readonly reservationGuests = computed<
    readonly ReservationDetailGuest[]
  >(() => (this.detail.hasValue() ? this.detail.value().guests : []));

  protected onGuestsMutated(): void {
    this.detail.reload();
  }

  protected readonly reservationMeals = computed<
    readonly ReservationDetailMeal[]
  >(() => (this.detail.hasValue() ? this.detail.value().meals : []));

  protected onMealsMutated(): void {
    this.detail.reload();
  }

  protected readonly reservationInvoices = computed<
    readonly ReservationDetailInvoice[]
  >(() => (this.detail.hasValue() ? this.detail.value().invoices : []));

  protected readonly reservationBills = computed<
    readonly ReservationDetailBill[]
  >(() => (this.detail.hasValue() ? this.detail.value().bills : []));

  protected readonly editableServiceQuantities = signal<
    ReadonlyMap<string, number>
  >(new Map());

  protected readonly editableVehicles = signal<readonly Vehicle[]>([]);

  // Vehicle rows are aggregated into service lines on submit; exclude
  // them from the other-services pass-through to avoid double-counting.
  private readonly vehicleGroupServiceIds = computed<ReadonlySet<string>>(
    () =>
      new Set([
        ...this.servicesStore.byGroup(ServiceGroup.Vehicles).map(s => s.id),
        ...this.servicesStore.byGroup(ServiceGroup.MotorHomes).map(s => s.id),
      ])
  );

  protected readonly requestedSpotsByGroup = computed<ReadonlyMap<
    string,
    number
  > | null>(() => {
    if (this.mode() !== "edit" || !this.detail.hasValue()) {
      return null;
    }
    const counts = new Map<string, number>();
    for (const item of this.detail.value().spotItems) {
      counts.set(item.spotGroupId, (counts.get(item.spotGroupId) ?? 0) + 1);
    }
    return counts;
  });

  protected readonly note = signal<string>("");
  protected readonly displayName = signal<string>("");
  protected readonly groupReservationId = signal<string | null>(null);

  private prefilled = false;
  private copyPrefilled = false;
  private spotPrefilled = false;
  private groupDatePrefilled = false;
  private readonly spotPrefillId: string | null;
  private readonly copyFromId: string | null;

  private readonly savedSnapshot = signal<string>("");

  private currentSnapshot(): string {
    const vehicleSnapshot = [...this.editableVehicles()]
      .map(v => `${v.persistentId ?? "new"}|${v.serviceId}|${v.plate}`)
      .sort();
    const serviceSnapshot = [...this.editableServiceQuantities().entries()]
      .map(([id, qty]) => `${id}=${qty}`)
      .sort();
    return JSON.stringify({
      from: this.fromDate()?.getTime() ?? null,
      to: this.toDate()?.getTime() ?? null,
      name: this.name(),
      surname: this.surname(),
      phone: this.phone(),
      email: this.email(),
      note: this.note(),
      displayName: this.displayName(),
      groupReservationId: this.groupReservationId(),
      spotIds: [...this.spotRows()].map(r => r.spotId).sort(),
      vehicles: vehicleSnapshot,
      services: serviceSnapshot,
    });
  }

  protected readonly isDirty = computed<boolean>(
    () => this.currentSnapshot() !== this.savedSnapshot()
  );

  private readonly groupDetail = httpResource<GroupReservationDetail>(() => {
    const id = this.groupReservationId();
    return id ? this.apiClient.url(`/group-reservations/${id}`) : undefined;
  });

  protected readonly groupOrganizerName = computed<string | null>(() =>
    this.groupDetail.hasValue() ? this.groupDetail.value().organizerName : null
  );

  constructor() {
    let spotPrefill: string | null = null;
    let copyFrom: string | null = null;
    if (this.mode() === "create") {
      const params = this.route.snapshot.queryParamMap;
      const grp = params.get("groupReservationId");
      if (grp) {
        this.groupReservationId.set(grp);
      }
      spotPrefill = params.get("spot");
      copyFrom = params.get("fromId");
    }
    this.spotPrefillId = spotPrefill;
    this.copyFromId = copyFrom;

    queueMicrotask(() => this.savedSnapshot.set(this.currentSnapshot()));

    effect(() => {
      if (
        this.prefilled ||
        this.mode() !== "edit" ||
        !this.detail.hasValue() ||
        this.spots().length === 0
      ) {
        return;
      }
      this.prefilled = true;

      const d = this.detail.value();
      this.fromDate.set(isoToDate(d.from));
      this.toDate.set(isoToDate(d.to));
      this.name.set(d.reservationMakerName);
      this.surname.set(d.reservationMakerSurname);
      this.phone.set(d.reservationMakerPhone);
      this.email.set(d.reservationMakerEmail);
      this.note.set(d.note ?? "");
      this.displayName.set(d.displayName ?? "");
      this.groupReservationId.set(d.groupReservationId);

      // Items with a null spotId are online bookings that picked only
      // group + quantity; surface only assigned ones in the picker.
      const spotItems = d.spotItems;
      this.spotRows.set(
        spotItems
          .filter(item => item.spotId !== null)
          .map<SpotRow>(item => ({
            id: crypto.randomUUID() as string,
            spotGroupId: item.spotGroupId,
            spotId: item.spotId,
            hasGivenKey: item.hasGivenKey,
            hasReturnedKeys: item.hasReturnedKeys,
          }))
      );

      const editable = new Map<string, number>();
      for (const item of d.serviceItems) {
        if (item.quantity > 0) {
          editable.set(item.serviceId, item.quantity);
        }
      }
      this.editableServiceQuantities.set(editable);

      // Fall back to "" / 0 when ServicesStore hasn't resolved yet; the
      // reactive read in the vehicles step picks up correct values later.
      const fromIso = d.from;
      const toIso = d.to;
      const nights = nightsBetweenIso(fromIso, toIso);
      const services = this.servicesStore.active();
      this.editableVehicles.set(
        d.vehicles.map<Vehicle>(v => {
          const svc = v.serviceId
            ? services.find(s => s.id === v.serviceId)
            : undefined;
          return {
            id: crypto.randomUUID() as string,
            persistentId: v.id,
            plate: v.registrationNumber,
            type: svc?.name ?? "",
            serviceId: v.serviceId ?? "",
            nights,
            ratePerNight: svc?.basePrice ?? 0,
          };
        })
      );

      queueMicrotask(() => this.savedSnapshot.set(this.currentSnapshot()));
    });

    // Copy-reservation prefill from ?fromId=<id>: seed editable fields
    // but reset vehicle persistentId so each row is created fresh.
    effect(() => {
      if (
        this.copyPrefilled ||
        this.mode() !== "create" ||
        !this.copyFromId ||
        !this.sourceDetail.hasValue() ||
        this.spots().length === 0
      ) {
        return;
      }
      this.copyPrefilled = true;

      const d = this.sourceDetail.value();
      this.fromDate.set(isoToDate(d.from));
      this.toDate.set(isoToDate(d.to));
      this.name.set(d.reservationMakerName);
      this.surname.set(d.reservationMakerSurname);
      this.phone.set(d.reservationMakerPhone);
      this.email.set(d.reservationMakerEmail);
      this.note.set(d.note ?? "");
      this.displayName.set(d.displayName ?? "");
      this.groupReservationId.set(d.groupReservationId);

      this.spotRows.set(
        d.spotItems
          .filter(item => item.spotId !== null)
          .map<SpotRow>(item => ({
            id: crypto.randomUUID() as string,
            spotGroupId: item.spotGroupId,
            spotId: item.spotId,
            hasGivenKey: item.hasGivenKey,
            hasReturnedKeys: item.hasReturnedKeys,
          }))
      );

      const editable = new Map<string, number>();
      for (const item of d.serviceItems) {
        if (item.quantity > 0) {
          editable.set(item.serviceId, item.quantity);
        }
      }
      this.editableServiceQuantities.set(editable);

      const nights = nightsBetweenIso(d.from, d.to);
      const services = this.servicesStore.active();
      this.editableVehicles.set(
        d.vehicles.map<Vehicle>(v => {
          const svc = v.serviceId
            ? services.find(s => s.id === v.serviceId)
            : undefined;
          return {
            id: crypto.randomUUID() as string,
            // Reusing the source's vehicle id would re-link to another
            // reservation's vehicle; backend assigns a fresh id on POST.
            persistentId: null,
            plate: v.registrationNumber,
            type: svc?.name ?? "",
            serviceId: v.serviceId ?? "",
            nights,
            ratePerNight: svc?.basePrice ?? 0,
          };
        })
      );

      // POST /guests needs the new reservation id, so stash for after create.
      this.guestsToCopy.set(d.guests);

      queueMicrotask(() => this.savedSnapshot.set(this.currentSnapshot()));
    });

    effect(() => {
      if (
        this.spotPrefilled ||
        this.mode() !== "create" ||
        !this.spotPrefillId ||
        this.spots().length === 0
      ) {
        return;
      }
      this.spotPrefilled = true;

      const spot = this.spots().find(s => s.id === this.spotPrefillId);
      if (!spot) {
        return;
      }
      this.spotRows.set([
        {
          id: crypto.randomUUID() as string,
          spotGroupId: spot.spotGroupId,
          spotId: spot.id,
        },
      ]);
      queueMicrotask(() => this.savedSnapshot.set(this.currentSnapshot()));
    });

    // When opened from a group reservation, default dates to the group's range.
    effect(() => {
      if (
        this.groupDatePrefilled ||
        this.mode() !== "create" ||
        !this.groupDetail.hasValue()
      ) {
        return;
      }
      this.groupDatePrefilled = true;

      const g = this.groupDetail.value();
      this.fromDate.set(isoToDate(g.from));
      this.toDate.set(isoToDate(g.to));
      queueMicrotask(() => this.savedSnapshot.set(this.currentSnapshot()));
    });
  }

  protected readonly activeStep = computed<StepDef>(
    () => this.visibleSteps()[this.activeStepIndex()] ?? STEPS[0]!
  );

  protected readonly canCancel = computed<boolean>(() => {
    if (this.mode() !== "edit" || !this.detail.hasValue()) {
      return false;
    }
    return CANCELLABLE_STATES.includes(this.detail.value().state);
  });

  protected readonly canCheckIn = computed<boolean>(() => {
    if (this.mode() !== "edit" || !this.detail.hasValue()) {
      return false;
    }
    return this.detail.value().state === ReservationState.Confirmed;
  });

  protected readonly stateLabel = computed<string>(() => {
    if (!this.detail.hasValue()) {
      return "";
    }
    switch (this.detail.value().state) {
      case ReservationState.Created:
        return "Vytvořena";
      case ReservationState.Confirmed:
        return "Potvrzena";
      case ReservationState.CheckedIn:
        return "Ubytováno";
      case ReservationState.Cancelled:
        return "Zrušena";
      case ReservationState.Completed:
        return "Dokončena";
    }
  });

  protected isStepDisabled(step: StepDef): boolean {
    if (this.mode() !== "create") {
      return false;
    }
    return step.editOnly;
  }

  protected readonly hasPrevEnabled = computed<boolean>(() => {
    const steps = this.visibleSteps();
    for (let i = this.activeStepIndex() - 1; i >= 0; i--) {
      const s = steps[i];
      if (s && !this.isStepDisabled(s)) {
        return true;
      }
    }
    return false;
  });

  protected readonly hasNextEnabled = computed<boolean>(() => {
    const steps = this.visibleSteps();
    for (let i = this.activeStepIndex() + 1; i < steps.length; i++) {
      const s = steps[i];
      if (s && !this.isStepDisabled(s)) {
        return true;
      }
    }
    return false;
  });

  protected setActiveIndex(idx: number): void {
    if (idx < 0 || idx >= this.visibleSteps().length) {
      return;
    }
    const step = this.visibleSteps()[idx];
    if (!step || this.isStepDisabled(step)) {
      return;
    }
    this.activeStepIndex.set(idx);
  }

  protected onPrev(): void {
    const steps = this.visibleSteps();
    for (let i = this.activeStepIndex() - 1; i >= 0; i--) {
      const s = steps[i];
      if (s && !this.isStepDisabled(s)) {
        this.setActiveIndex(i);
        return;
      }
    }
  }

  protected onNext(): void {
    const steps = this.visibleSteps();
    for (let i = this.activeStepIndex() + 1; i < steps.length; i++) {
      const s = steps[i];
      if (s && !this.isStepDisabled(s)) {
        this.setActiveIndex(i);
        return;
      }
    }
  }

  protected readonly canSubmit = computed<boolean>(() => {
    if (this.submitting()) {
      return false;
    }
    return this.collectValidationErrors().length === 0;
  });

  // Saving a Created reservation transitions it to Confirmed.
  protected readonly submitLabel = computed<string>(() => {
    if (this.mode() === "create") {
      return "Vytvořit rezervaci";
    }
    if (
      this.detail.hasValue() &&
      this.detail.value().state === ReservationState.Created
    ) {
      return "Uložit a potvrdit";
    }
    return "Uložit změny";
  });

  private hasSpotCountMismatch(): boolean {
    const requested = this.requestedSpotsByGroup();
    if (requested === null || requested.size === 0) {
      return false;
    }
    const assignedByGroup = new Map<string, number>();
    for (const row of this.spotRows()) {
      if (row.spotGroupId && row.spotId !== null) {
        assignedByGroup.set(
          row.spotGroupId,
          (assignedByGroup.get(row.spotGroupId) ?? 0) + 1
        );
      }
    }
    for (const [groupId, count] of requested) {
      if ((assignedByGroup.get(groupId) ?? 0) !== count) {
        return true;
      }
    }
    for (const [groupId, count] of assignedByGroup) {
      if (count > 0 && !requested.has(groupId)) {
        return true;
      }
    }
    return false;
  }

  private collectValidationErrors(): readonly string[] {
    const errors: string[] = [];
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t) {
      errors.push("Vyplňte datum příjezdu a odjezdu.");
    } else if (f.getTime() >= t.getTime()) {
      errors.push("Datum odjezdu musí být pozdější než datum příjezdu.");
    }
    if (this.name().trim().length === 0) {
      errors.push("Vyplňte jméno.");
    }
    if (this.surname().trim().length === 0) {
      errors.push("Vyplňte příjmení.");
    }
    // Strip whitespace before matching - staff write phones like "+420 777 987 428".
    if (!PHONE_RE.test(this.phone().replace(/\s+/g, ""))) {
      errors.push("Vyplňte platné telefonní číslo (9 až 15 číslic).");
    }
    if (!EMAIL_RE.test(this.email().trim())) {
      errors.push("Vyplňte platnou e-mailovou adresu.");
    }
    const rows = this.spotRows();
    if (rows.some(r => !r.spotGroupId || !r.spotId)) {
      errors.push("Doplňte u všech řádků skupinu i konkrétní chatu.");
    }
    return errors;
  }

  private buildServiceLines(): readonly ReservationServiceRequest[] {
    const skip = this.vehicleGroupServiceIds();
    const out: ReservationServiceRequest[] = [];

    for (const [serviceId, quantity] of this.editableServiceQuantities()) {
      if (quantity > 0 && !skip.has(serviceId)) {
        out.push({
          serviceId,
          quantity,
          recapSingleQuantity: 0,
          recapDayQuantity: 0,
        });
      }
    }

    const vehicleCounts = new Map<string, number>();
    for (const v of this.editableVehicles()) {
      if (v.serviceId.length === 0) {
        continue;
      }
      vehicleCounts.set(v.serviceId, (vehicleCounts.get(v.serviceId) ?? 0) + 1);
    }
    for (const [serviceId, quantity] of vehicleCounts) {
      out.push({
        serviceId,
        quantity,
        recapSingleQuantity: 0,
        recapDayQuantity: 0,
      });
    }

    return out;
  }

  private buildVehicleRequest(): readonly ReservationVehicleRequest[] {
    return this.editableVehicles().map(v => ({
      id: v.persistentId,
      registrationNumber: v.plate,
    }));
  }

  protected onSubmit(): void {
    const errs = this.collectValidationErrors();
    this.validationErrors.set(errs);
    if (errs.length > 0) {
      return;
    }
    if (this.hasSpotCountMismatch()) {
      this.confirmService.confirm({
        message:
          "Počet vybraných chat neodpovídá původně požadovanému počtu. Opravdu chcete uložit?",
        header: "Neshoda v počtu chat",
        icon: "pi pi-exclamation-triangle",
        acceptLabel: "Ano, uložit",
        rejectLabel: "Zpět",
        accept: () => this.performSubmit(),
      });
      return;
    }
    this.performSubmit();
  }

  private performSubmit(): void {
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t) {
      return;
    }

    const note = this.note().trim();
    const displayName = this.displayName().trim();
    const body: ReservationRequest = {
      from: dateToIso(f),
      to: dateToIso(t),
      name: this.name().trim(),
      surname: this.surname().trim(),
      email: this.email().trim(),
      phone: this.phone().replace(/\s+/g, ""),
      spotIds: this.spotRows()
        .map(r => r.spotId)
        .filter((id): id is string => typeof id === "string"),
      services: this.buildServiceLines(),
      vehicles: this.buildVehicleRequest(),
      note: note.length > 0 ? note : null,
      displayName: displayName.length > 0 ? displayName : null,
      groupReservationId: this.groupReservationId(),
    };

    const id = this.reservationId();
    this.submitting.set(true);
    this.errorMessage.set(null);

    const isCreate = id === null;
    const request$: Observable<string | void> = isCreate
      ? this.apiClient.post<string>("/reservations", body)
      : this.apiClient.put<void>(`/reservations/${id}`, body);

    request$.subscribe({
      next: result => {
        if (isCreate && typeof result === "string") {
          this.onCreateSuccess(result);
        } else {
          this.onUpdateSuccess();
        }
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  private onCreateSuccess(id: string): void {
    this.validationErrors.set([]);
    this.errorMessage.set(null);
    this.savedSnapshot.set(this.currentSnapshot());

    const sourceGuests = this.guestsToCopy();
    if (sourceGuests.length === 0) {
      this.submitting.set(false);
      this.messageService.add({
        severity: "success",
        summary: "Hotovo",
        detail: "Rezervace byla vytvořena.",
      });
      void this.router.navigate(
        ["/staff/auth/desktop/reservations", id, "edit"],
        { replaceUrl: true }
      );
      return;
    }

    this.copyGuests(id, sourceGuests);
  }

  private copyGuests(
    newReservationId: string,
    sourceGuests: readonly ReservationDetailGuest[]
  ): void {
    const requests = sourceGuests
      .map(g => this.buildGuestCopyRequest(newReservationId, g))
      .filter((r): r is GuestRequest => r !== null);
    const skipped = sourceGuests.length - requests.length;

    if (requests.length === 0) {
      this.submitting.set(false);
      this.guestsToCopy.set([]);
      this.messageService.add({
        severity: "warn",
        summary: "Rezervace vytvořena",
        detail: `Žádný host nebyl zkopírován (${skipped} přeskočeno – chybí doklad).`,
      });
      void this.router.navigate(
        ["/staff/auth/desktop/reservations", newReservationId, "edit"],
        { replaceUrl: true }
      );
      return;
    }

    forkJoin(
      requests.map(r =>
        this.guestsApi.create(r).pipe(catchError(() => of(null)))
      )
    ).subscribe(results => {
      this.submitting.set(false);
      this.guestsToCopy.set([]);
      const created = results.filter(x => x !== null).length;
      const failed = results.length - created;
      const parts: string[] = [];
      parts.push(
        created === 1 ? "1 host zkopírován" : `${created} hostů zkopírováno`
      );
      if (skipped > 0) {
        parts.push(`${skipped} přeskočeno (chybí doklad)`);
      }
      if (failed > 0) {
        parts.push(`${failed} se nepodařilo`);
      }
      this.messageService.add({
        severity: failed > 0 ? "warn" : "success",
        summary: "Rezervace vytvořena",
        detail: parts.join(" · "),
      });
      void this.router.navigate(
        ["/staff/auth/desktop/reservations", newReservationId, "edit"],
        { replaceUrl: true }
      );
    });
  }

  // Returns null when required document fields are missing on the
  // source; backend rejects those so we skip rather than fail the batch.
  private buildGuestCopyRequest(
    newReservationId: string,
    g: ReservationDetailGuest
  ): GuestRequest | null {
    if (g.documentType === null) {
      return null;
    }
    const docNumber = g.documentNumber?.trim() ?? "";
    if (docNumber.length === 0) {
      return null;
    }
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t) {
      return null;
    }
    return {
      reservationId: newReservationId,
      billId: null,
      paysRecreationFee: null,
      firstName: g.firstName,
      lastName: g.lastName,
      nationalityId: g.nationalityId,
      dateOfBirth: g.dateOfBirth,
      documentType: g.documentType,
      documentNumber: docNumber,
      address: g.address,
      reasonOfStay: g.reasonOfStay,
      stayDateRange: { from: dateToIso(f), to: dateToIso(t) },
      visaNumber: g.visaNumber,
      note: g.note,
      scartation: g.scartation,
      checkInAt: null,
      checkOutAt: null,
      signaturePngBase64: null,
    };
  }

  private onUpdateSuccess(): void {
    this.submitting.set(false);
    this.validationErrors.set([]);
    this.errorMessage.set(null);
    this.detail.reload();
    this.messageService.add({
      severity: "success",
      summary: "Hotovo",
      detail: "Rezervace byla aktualizována.",
    });
    this.savedSnapshot.set(this.currentSnapshot());
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    const status =
      typeof err === "object" && err !== null && "status" in err
        ? (err as { status: unknown }).status
        : null;
    if (status === 409) {
      this.errorMessage.set(
        "Vybraná místa nejsou v zadaném termínu dostupná. Změňte termín nebo výběr chat."
      );
      return;
    }
    if (status === 400) {
      this.errorMessage.set("Zkontrolujte prosím vyplněné údaje.");
      return;
    }
    if (status === 404) {
      this.errorMessage.set("Některý z odkazovaných záznamů neexistuje.");
      return;
    }
    this.errorMessage.set("Něco se pokazilo. Zkuste to prosím znovu.");
  }

  protected onCancelReservation(): void {
    const id = this.reservationId();
    if (!id || !this.detail.hasValue()) {
      return;
    }
    const d = this.detail.value();
    const fullName =
      `${d.reservationMakerName} ${d.reservationMakerSurname}`.trim();
    const paidInvoices = d.invoices.filter(
      i => i.status === InvoiceStatus.Paid
    );
    const paidNote =
      paidInvoices.length > 0
        ? ` Rezervace má ${paidInvoices.length === 1 ? "zaplacenou fakturu" : `${paidInvoices.length} zaplacených faktur`} (${paidInvoices
            .map(i => i.number ?? "—")
            .join(", ")}), kterou bude potřeba vypořádat samostatně.`
        : "";
    this.confirmService.confirm({
      message: `Zrušit rezervaci ${fullName}? Tuto akci nelze vrátit zpět.${paidNote}`,
      header: "Zrušit rezervaci",
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Zrušit rezervaci",
      rejectLabel: "Zpět",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.submitting.set(true);
        this.errorMessage.set(null);
        this.apiClient.post<void>(`/reservations/${id}/cancel`, {}).subscribe({
          next: () => {
            this.submitting.set(false);
            this.detail.reload();
            this.messageService.add({
              severity: "success",
              summary: "Hotovo",
              detail: "Rezervace byla zrušena.",
            });
            this.savedSnapshot.set(this.currentSnapshot());
          },
          error: (err: unknown) => {
            this.submitting.set(false);
            const status =
              typeof err === "object" && err !== null && "status" in err
                ? (err as { status: unknown }).status
                : null;
            const detail =
              status === 409
                ? "Rezervaci v tomto stavu nelze zrušit."
                : "Rezervaci se nepodařilo zrušit.";
            this.messageService.add({
              severity: "error",
              summary: "Chyba",
              detail,
            });
          },
        });
      },
    });
  }

  protected onCheckIn(): void {
    const id = this.reservationId();
    if (!id || this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.apiClient.post<void>(`/reservations/${id}/check-in`, null).subscribe({
      next: () => {
        this.submitting.set(false);
        this.detail.reload();
        this.messageService.add({
          severity: "success",
          summary: "Hotovo",
          detail: "Rezervace ubytována.",
        });
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const status =
          typeof err === "object" && err !== null && "status" in err
            ? (err as { status: unknown }).status
            : null;
        const detail =
          status === 409
            ? "Rezervaci v tomto stavu nelze ubytovat."
            : "Rezervaci se nepodařilo ubytovat.";
        this.messageService.add({
          severity: "error",
          summary: "Chyba",
          detail,
        });
      },
    });
  }

  protected onCancelNavigate(): void {
    if (this.isDirty()) {
      this.confirmService.confirm({
        message: "Opustit stránku? Neuložené změny budou ztraceny.",
        header: "Opustit stránku",
        icon: "pi pi-exclamation-triangle",
        acceptLabel: "Opustit",
        rejectLabel: "Zůstat",
        accept: () => {
          void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
        },
      });
      return;
    }
    void this.router.navigate(["/staff/auth/desktop/reservation-plan"]);
  }
}
