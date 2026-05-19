import { DOCUMENT } from "@angular/common";
import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  LOCALE_ID,
  signal,
} from "@angular/core";

import { ConfirmationService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { InputGroupModule } from "primeng/inputgroup";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { TagModule } from "primeng/tag";
import { firstValueFrom } from "rxjs";

import { ApiClient } from "../../../core/api/api-client";
import type { ApiError } from "../../../core/api/api-error";
import { isoToDate } from "../../../shared/date-iso";
import { PublicReservationsApi } from "../api/public-reservations.api";
import type {
  ReservationForGuestResponse,
  ReservationGuestMealAmount,
} from "../api/public-reservations.types";

type CottageRow = {
  spotGroupId: string;
  name: string;
  qty: number;
  code: string;
  assigned: readonly string[];
};

type MealRow = {
  date: string;
  breakfast: number;
  lunch: number;
  lunchPackage: number;
  dinner: number;
};

function sumMealAmount(a: ReservationGuestMealAmount): number {
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

type TagSeverity = "success" | "info" | "warn" | "danger" | "secondary";

const CHECK_IN_FROM = "od 14:00";
const CHECK_OUT_BY = "do 10:00";

@Component({
  selector: "kemp-is-reservation-detail",
  imports: [
    ButtonModule,
    ConfirmDialogModule,
    InputGroupModule,
    InputTextModule,
    MessageModule,
    TagModule,
  ],
  templateUrl: "./reservation-detail.page.html",
  styleUrl: "./reservation-detail.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService],
})
export class ReservationDetailPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(PublicReservationsApi);
  private readonly confirmService = inject(ConfirmationService);
  private readonly document = inject(DOCUMENT);
  protected readonly locale = inject(LOCALE_ID);

  readonly id = input.required<string>();
  readonly secret = input.required<string>();

  protected readonly resource = httpResource<ReservationForGuestResponse>(
    () => {
      const params = new URLSearchParams({ secret: this.secret() });
      return `${this.apiClient.url(`/reservations/${this.id()}/guest`)}?${params.toString()}`;
    }
  );

  // resource.value() throws while pending or in error; gate access via hasValue().
  private readonly reservation = computed<ReservationForGuestResponse | null>(
    () => (this.resource.hasValue() ? this.resource.value() : null)
  );

  protected readonly checkInFrom = CHECK_IN_FROM;
  protected readonly checkOutBy = CHECK_OUT_BY;

  protected readonly nights = computed(() => {
    const r = this.reservation();
    if (!r) {
      return 0;
    }
    const a = isoToDate(r.from);
    const b = isoToDate(r.to);
    if (!a || !b) {
      return 0;
    }
    return Math.max(0, Math.round((b.getTime() - a.getTime()) / 86_400_000));
  });

  protected readonly cottages = computed<readonly CottageRow[]>(() => {
    const r = this.reservation();
    if (!r) {
      return [];
    }
    const map = new Map<
      string,
      { name: string; qty: number; code: string; assigned: string[] }
    >();
    for (const item of r.spotItems) {
      const existing = map.get(item.spotGroupId);
      if (existing) {
        existing.qty += 1;
        if (item.spotName) {
          existing.assigned.push(item.spotName);
        }
      } else {
        const firstLetter = item.spotGroupName.match(/\p{L}/u)?.[0] ?? "?";
        map.set(item.spotGroupId, {
          name: item.spotGroupName,
          qty: 1,
          code: firstLetter.toUpperCase(),
          assigned: item.spotName ? [item.spotName] : [],
        });
      }
    }
    return Array.from(map.entries()).map(([spotGroupId, row]) => ({
      spotGroupId,
      ...row,
    }));
  });

  protected readonly mealRows = computed<readonly MealRow[]>(() => {
    const r = this.reservation();
    if (!r) {
      return [];
    }
    return r.meals.map(m => ({
      date: m.date,
      breakfast: sumMealAmount(m.breakfast),
      lunch: sumMealAmount(m.lunch),
      lunchPackage: sumMealAmount(m.lunchPackage),
      dinner: sumMealAmount(m.dinner),
    }));
  });

  protected readonly totalPortions = computed(() =>
    this.mealRows().reduce(
      (s, m) => s + m.breakfast + m.lunch + m.lunchPackage + m.dinner,
      0
    )
  );

  protected readonly stateTag = computed<{
    severity: TagSeverity;
    label: string;
    icon: string;
  }>(() => {
    const raw = this.reservation()?.state ?? "";
    switch (raw) {
      case "Created":
        return {
          severity: "warn",
          label: $localize`:@@reservation-detail.state.created:Čeká na potvrzení`,
          icon: "pi pi-clock",
        };
      case "Confirmed":
        return {
          severity: "success",
          label: $localize`:@@reservation-detail.state.confirmed:Potvrzeno`,
          icon: "pi pi-check-circle",
        };
      case "CheckedIn":
        return {
          severity: "info",
          label: $localize`:@@reservation-detail.state.checked-in:Pobyt probíhá`,
          icon: "pi pi-home",
        };
      case "Cancelled":
        return {
          severity: "danger",
          label: $localize`:@@reservation-detail.state.cancelled:Zrušeno`,
          icon: "pi pi-times-circle",
        };
      case "Completed":
        return {
          severity: "secondary",
          label: $localize`:@@reservation-detail.state.completed:Dokončeno`,
          icon: "pi pi-flag",
        };
      default:
        return {
          severity: "secondary",
          label: raw,
          icon: "pi pi-circle-fill",
        };
    }
  });

  // Built relative to current location so any locale prefix (/en/...) is preserved.
  protected readonly checkInUrl = computed(() => {
    const loc = this.document.location;
    const path = loc.pathname.replace(/\/+$/, "");
    const params = new URLSearchParams({ secret: this.secret() });
    return `${loc.origin}${path}/check-in?${params.toString()}`;
  });

  protected readonly copied = signal(false);
  private copyResetTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly canCancel = computed(() => {
    const state = this.reservation()?.state;
    return state === "Created" || state === "Confirmed";
  });

  protected readonly cancelling = signal(false);
  protected readonly cancelError = signal<ApiError | null>(null);

  protected billLabel(kind: string): string {
    switch (kind) {
      case "Deposit":
        return $localize`:@@reservation-detail.bill.deposit:Záloha`;
      case "Balance":
        return $localize`:@@reservation-detail.bill.balance:Doplatek`;
      case "TouristTax":
        return $localize`:@@reservation-detail.bill.tourist-tax:Pobytová taxa`;
      case "Cancellation":
        return $localize`:@@reservation-detail.bill.cancellation:Storno`;
      default:
        return kind;
    }
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
      weekday: "short",
      day: "numeric",
      month: "short",
    });
  }

  protected readonly printLabel = $localize`:@@reservation-detail.print:Tisk`;
  protected readonly cancelLabel = $localize`:@@reservation-detail.cancel:Zrušit rezervaci`;
  protected readonly downloadLabel = $localize`:@@reservation-detail.download-invoice:Stáhnout fakturu`;
  protected readonly copyLabel = $localize`:@@reservation-detail.copy:Kopírovat`;
  protected readonly copiedLabel = $localize`:@@reservation-detail.copied:Zkopírováno`;
  protected readonly copyCheckInLabel = $localize`:@@reservation-detail.copy-checkin:Kopírovat odkaz pro online check-in`;
  protected readonly linkCopiedLabel = $localize`:@@reservation-detail.copy-checkin.copied:Odkaz zkopírován`;
  protected readonly checkInFieldLabel = $localize`:@@reservation-detail.copy-field:Odkaz pro online check-in`;

  protected onCopy(): void {
    if (this.copyResetTimer !== null) {
      clearTimeout(this.copyResetTimer);
    }
    void navigator.clipboard.writeText(this.checkInUrl());
    this.copied.set(true);
    this.copyResetTimer = setTimeout(() => {
      this.copied.set(false);
      this.copyResetTimer = null;
    }, 1800);
  }

  protected onPrint(): void {
    window.print();
  }

  protected onCancel(): void {
    if (!this.canCancel() || this.cancelling()) {
      return;
    }
    this.confirmService.confirm({
      header: $localize`:@@reservation-detail.cancel.confirm.header:Zrušit rezervaci?`,
      message: $localize`:@@reservation-detail.cancel.confirm.message:Tuto akci nelze vrátit zpět. Opravdu chcete rezervaci zrušit?`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: $localize`:@@reservation-detail.cancel.confirm.accept:Ano, zrušit`,
      rejectLabel: $localize`:@@reservation-detail.cancel.confirm.reject:Zpět`,
      acceptButtonStyleClass: "p-button-danger",
      rejectButtonStyleClass: "p-button-text",
      defaultFocus: "reject",
      accept: () => {
        void this.doCancel();
      },
    });
  }

  private async doCancel(): Promise<void> {
    this.cancelling.set(true);
    this.cancelError.set(null);
    try {
      await firstValueFrom(
        this.api.cancelReservationForGuest(this.id(), this.secret())
      );
      this.resource.reload();
    } catch (err) {
      this.cancelError.set(err as ApiError);
    } finally {
      this.cancelling.set(false);
    }
  }
}
