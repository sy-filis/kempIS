import { httpResource } from "@angular/common/http";
import { LOCALE_ID } from "@angular/core";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  applyEach,
  email,
  form,
  FormField,
  maxLength,
  min,
  pattern,
  provideSignalFormsConfig,
  required,
  submit,
  validate,
} from "@angular/forms/signals";

import { ButtonModule } from "primeng/button";
import { CardModule } from "primeng/card";
import { DatePickerModule } from "primeng/datepicker";
import { FloatLabelModule } from "primeng/floatlabel";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { StepperModule } from "primeng/stepper";
import { TagModule } from "primeng/tag";
import { TextareaModule } from "primeng/textarea";
import { TooltipModule } from "primeng/tooltip";
import { firstValueFrom } from "rxjs";

import { equal } from "../../../../utils/deepEqual";
import { ApiClient } from "../../../core/api/api-client";
import type { ApiError } from "../../../core/api/api-error";
import { CAMP_IDENTITY } from "../../../core/camp/camp-identity.token";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import { PublicReservationsApi } from "../api/public-reservations.api";
import type {
  AvailabilityResponse,
  CreateWebReservationRequest,
  CreateWebReservationResponse,
  RequestedSpotGroupDto,
} from "../api/public-reservations.types";

type RequestedSpot = { spotGroupId: string; quantity: number };

type DateRangeModel = { from: string; to: string };
type SpotsModel = { requestedSpots: RequestedSpot[] };
type ContactModel = {
  name: string;
  surname: string;
  email: string;
  phone: string;
  note: string;
};

const SPOT_SWATCHES = [
  "oklch(0.72 0.05 80)",
  "oklch(0.50 0.06 65)",
  "oklch(0.62 0.04 150)",
  "oklch(0.67 0.07 220)",
  "oklch(0.78 0.04 60)",
  "oklch(0.55 0.05 30)",
];

@Component({
  selector: "kemp-is-new-reservation",
  imports: [
    FormsModule,
    FormField,
    ButtonModule,
    CardModule,
    DatePickerModule,
    FloatLabelModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    StepperModule,
    TagModule,
    TextareaModule,
    TooltipModule,
  ],
  templateUrl: "./new-reservation.page.html",
  styleUrl: "./new-reservation.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    provideSignalFormsConfig({
      classes: {
        "kemp-touched": field => field.state().touched(),
      },
    }),
  ],
})
export class NewReservationPage {
  protected readonly api = inject(PublicReservationsApi);
  protected readonly apiClient = inject(ApiClient);
  protected readonly locale = inject(LOCALE_ID);
  protected readonly camp = inject(CAMP_IDENTITY);

  readonly groupId = input<string | undefined>(undefined);
  readonly secret = input<string | undefined>(undefined);

  protected readonly dateModel = signal<DateRangeModel>({ from: "", to: "" });
  protected readonly spotsModel = signal<SpotsModel>({ requestedSpots: [] });
  protected readonly contactModel = signal<ContactModel>({
    name: "",
    surname: "",
    email: "",
    phone: "",
    note: "",
  });

  protected readonly rangeDates = signal<Date[] | null>(null);

  protected readonly currentStep = signal<number>(1);

  protected readonly submitting = signal(false);
  protected readonly submitError = signal<ApiError | null>(null);
  protected readonly submitted = signal<CreateWebReservationResponse | null>(
    null
  );

  protected readonly today = new Date();

  protected readonly dateRange = computed(
    () => {
      const m = this.dateModel();
      return m.from && m.to && m.from <= m.to
        ? { from: m.from, to: m.to }
        : null;
    },
    { equal }
  );

  protected readonly nights = computed(() => {
    const dr = this.dateRange();
    if (!dr) {
      return 0;
    }
    const a = isoToDate(dr.from);
    const b = isoToDate(dr.to);
    if (!a || !b) {
      return 0;
    }
    const ms = b.getTime() - a.getTime();
    return Math.max(0, Math.round(ms / 86_400_000));
  });

  protected readonly availability = httpResource<AvailabilityResponse>(() => {
    const dr = this.dateRange();
    return dr
      ? `${this.apiClient.url("/availability")}?from=${dr.from}&to=${dr.to}`
      : undefined;
  });

  // availability.value() throws while pending or in error; gate via hasValue().
  private readonly availabilitySafe = computed<AvailabilityResponse | null>(
    () => (this.availability.hasValue() ? this.availability.value() : null)
  );

  protected readonly availableByGroupId = computed(() => {
    const av = this.availabilitySafe();
    const map = new Map<string, number>();
    if (!av) {
      return map;
    }
    for (const g of av.spotGroups) {
      map.set(g.spotGroupId, g.available);
    }
    return map;
  });

  protected readonly totalCottages = computed(() =>
    this.spotsModel().requestedSpots.reduce((sum, r) => sum + r.quantity, 0)
  );

  protected readonly selectedPicks = computed(() => {
    const av = this.availabilitySafe();
    if (!av) {
      return [];
    }
    const byId = new Map(av.spotGroups.map(g => [g.spotGroupId, g.name]));
    return this.spotsModel()
      .requestedSpots.filter(r => r.quantity > 0)
      .map(r => ({
        spotGroupId: r.spotGroupId,
        name: byId.get(r.spotGroupId) ?? "",
        quantity: r.quantity,
      }));
  });

  protected readonly bookingReference = computed(
    () => this.submitted()?.number ?? ""
  );

  protected readonly dateForm = form(this.dateModel, s => {
    required(s.from, {
      message: $localize`:@@form.dates.from-required:Vyberte datum příjezdu`,
    });
    required(s.to, {
      message: $localize`:@@form.dates.to-required:Vyberte datum odjezdu`,
    });
    validate(s.from, ({ value }) => {
      const today = dateToIso(new Date());
      return value() && value() < today
        ? {
            kind: "past",
            message: $localize`:@@form.dates.past:Datum nemůže být v minulosti`,
          }
        : undefined;
    });
    validate(s.to, ({ value, valueOf }) => {
      const fromVal = valueOf(s.from);
      return fromVal && value() && fromVal > value()
        ? {
            kind: "order",
            message: $localize`:@@form.dates.order:Datum odjezdu musí být po datu příjezdu`,
          }
        : undefined;
    });
  });

  protected readonly spotsForm = form(this.spotsModel, s => {
    validate(s.requestedSpots, ({ value }) =>
      value().reduce((sum, r) => sum + r.quantity, 0) === 0
        ? {
            kind: "noSpots",
            message: $localize`:@@form.spots.required:Vyberte aspoň jedno místo`,
          }
        : undefined
    );

    applyEach(s.requestedSpots, item => {
      min(item.quantity, 0);
      validate(item.quantity, ({ value, valueOf }) => {
        const groupId = valueOf(item.spotGroupId);
        const max = this.availableByGroupId().get(groupId);
        return max !== undefined && value() > max
          ? {
              kind: "exceedsAvailable",
              message: $localize`:@@form.spots.exceeds:Překračuje volnou kapacitu`,
            }
          : undefined;
      });
    });
  });

  protected readonly contactForm = form(this.contactModel, s => {
    required(s.name, {
      message: $localize`:@@form.name.required:Vyplňte jméno`,
    });
    required(s.surname, {
      message: $localize`:@@form.surname.required:Vyplňte příjmení`,
    });
    required(s.email, {
      message: $localize`:@@form.email.required:Vyplňte e-mail`,
    });
    email(s.email, {
      message: $localize`:@@form.email.invalid:Neplatný e-mail`,
    });
    required(s.phone, {
      message: $localize`:@@form.phone.required:Vyplňte telefon`,
    });
    pattern(s.phone, /^\+?[0-9 ]{9,15}$/, {
      message: $localize`:@@form.phone.invalid:Neplatný formát telefonu`,
    });
    maxLength(s.note, 500);
  });

  protected readonly step1Valid = computed(() => {
    const f = this.dateForm;
    return (
      f.from().errors().length === 0 &&
      f.to().errors().length === 0 &&
      this.dateRange() !== null
    );
  });

  protected readonly step2Valid = computed(() => {
    if (!this.step1Valid()) {
      return false;
    }
    if (this.availability.isLoading() || this.availability.error()) {
      return false;
    }
    const spotsField = this.spotsForm.requestedSpots();
    if (spotsField.errors().length > 0) {
      return false;
    }
    const items = this.spotsModel().requestedSpots;
    for (let i = 0; i < items.length; i++) {
      const itemField = this.spotsForm.requestedSpots[i];
      if (itemField && itemField.quantity().errors().length > 0) {
        return false;
      }
    }
    return true;
  });

  protected readonly step3Valid = computed(() => {
    const f = this.contactForm;
    return (
      f.name().errors().length === 0 &&
      f.surname().errors().length === 0 &&
      f.email().errors().length === 0 &&
      f.phone().errors().length === 0 &&
      f.note().errors().length === 0
    );
  });

  protected readonly canContinue = computed(() => {
    switch (this.currentStep()) {
      case 1:
        return this.step1Valid();
      case 2:
        return this.step2Valid();
      case 3:
        return this.step3Valid();
      default:
        return false;
    }
  });

  protected readonly editDatesLabel = $localize`:@@new-reservation.cottages.edit-dates:Změnit termín`;
  protected readonly backLabel = $localize`:@@new-reservation.back:Zpět`;
  protected readonly manageLabel = $localize`:@@new-reservation.confirm.manage:Otevřít rezervaci`;

  protected readonly continueLabel = computed(() => {
    switch (this.currentStep()) {
      case 1:
        return $localize`:@@new-reservation.continue.dates:Pokračovat`;
      case 2:
        return $localize`:@@new-reservation.continue.cottages:Pokračovat na údaje`;
      case 3:
        return $localize`:@@new-reservation.continue.confirm:Potvrdit rezervaci`;
      default:
        return "";
    }
  });

  constructor() {
    effect(() => {
      const r = this.rangeDates();
      const start = r?.[0];
      const end = r?.[1];
      if (start && end) {
        const fromIso = dateToIso(start);
        const toIso = dateToIso(end);
        this.dateModel.update(m =>
          m.from === fromIso && m.to === toIso
            ? m
            : { from: fromIso, to: toIso }
        );
      } else if (!r || r.length === 0) {
        this.dateModel.update(m =>
          m.from === "" && m.to === "" ? m : { from: "", to: "" }
        );
      }
    });

    effect(() => {
      const groups = this.dateRange()
        ? (this.availabilitySafe()?.spotGroups ?? [])
        : [];
      const current = this.spotsModel().requestedSpots;

      if (
        groups.length === current.length &&
        groups.every((g, i) => current[i]?.spotGroupId === g.spotGroupId)
      ) {
        return;
      }

      const next: RequestedSpot[] = groups.map(g => {
        const existing = current.find(r => r.spotGroupId === g.spotGroupId);
        return {
          spotGroupId: g.spotGroupId,
          quantity: existing?.quantity ?? 0,
        };
      });
      this.spotsModel.set({ requestedSpots: next });
    });
  }

  protected goToStep(step: number): void {
    if (step < 1 || step > 4) {
      return;
    }
    if (step <= this.currentStep()) {
      this.currentStep.set(step);
      return;
    }
    if (step === 2 && !this.step1Valid()) {
      return;
    }
    if (step === 3 && !(this.step1Valid() && this.step2Valid())) {
      return;
    }
    if (step === 4) {
      return;
    }
    this.currentStep.set(step);
  }

  protected onContinue(): void {
    const step = this.currentStep();
    if (step === 3) {
      this.onSubmit();
      return;
    }
    if (step < 3 && this.canContinue()) {
      this.currentStep.set(step + 1);
    }
  }

  protected onBack(): void {
    if (this.currentStep() > 1 && !this.submitted()) {
      this.currentStep.update(s => s - 1);
    }
  }

  protected setQuantity(spotGroupId: string, quantity: number | null): void {
    const q = Math.max(0, quantity ?? 0);
    this.spotsModel.update(m => ({
      requestedSpots: m.requestedSpots.map(r =>
        r.spotGroupId === spotGroupId ? { ...r, quantity: q } : r
      ),
    }));
  }

  protected quantityFor(spotGroupId: string): number {
    return (
      this.spotsModel().requestedSpots.find(r => r.spotGroupId === spotGroupId)
        ?.quantity ?? 0
    );
  }

  protected setNote(value: string | null): void {
    const next = value ?? "";
    this.contactModel.update(m => (m.note === next ? m : { ...m, note: next }));
  }

  protected swatchFor(index: number): string {
    return SPOT_SWATCHES[index % SPOT_SWATCHES.length] ?? SPOT_SWATCHES[0]!;
  }

  protected formatDate(iso: string): string {
    const d = isoToDate(iso);
    if (!d) {
      return "";
    }
    return d.toLocaleDateString(this.locale, {
      weekday: "short",
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  }

  protected formatDateShort(iso: string): string {
    const d = isoToDate(iso);
    if (!d) {
      return "";
    }
    return d.toLocaleDateString(this.locale, {
      day: "numeric",
      month: "short",
    });
  }

  protected formatEventRange(
    startsAtUtc: string,
    endsAtUtc: string | null
  ): string {
    const fmt = (iso: string): string =>
      new Date(iso).toLocaleDateString(this.locale, {
        day: "numeric",
        month: "long",
        year: "numeric",
      });
    const start = fmt(startsAtUtc);
    return endsAtUtc ? `${start} – ${fmt(endsAtUtc)}` : start;
  }

  protected onSubmit(): void {
    submit(this.contactForm, async () => {
      this.submitting.set(true);
      this.submitError.set(null);
      try {
        const dm = this.dateModel();
        const sm = this.spotsModel();
        const cm = this.contactModel();
        const requestedSpots: RequestedSpotGroupDto[] = sm.requestedSpots
          .filter(r => r.quantity > 0)
          .map(r => ({ spotGroupId: r.spotGroupId, quantity: r.quantity }));

        const body: CreateWebReservationRequest = {
          name: cm.name,
          surname: cm.surname,
          email: cm.email,
          phone: cm.phone,
          from: dm.from,
          to: dm.to,
          requestedSpots,
          ...(cm.note ? { note: cm.note } : {}),
          ...(this.groupId() ? { groupReservationId: this.groupId() } : {}),
          ...(this.secret() ? { groupReservationSecret: this.secret() } : {}),
        };

        const response = await firstValueFrom(
          this.api.createWebReservation(body)
        );
        this.submitted.set(response);
        this.currentStep.set(4);
      } catch (err) {
        this.submitError.set(err as ApiError);
      } finally {
        this.submitting.set(false);
      }
    });
  }
}
