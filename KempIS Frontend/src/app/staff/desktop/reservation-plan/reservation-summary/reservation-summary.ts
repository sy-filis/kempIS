import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { Router } from "@angular/router";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { ApiClient } from "../../../../core/api/api-client";
import { AuthService } from "../../../../core/auth/auth.service";
import { Roles } from "../../../../core/auth/roles";
import { RefreshController } from "../../../../core/refresh/refresh-controller";
import { SpotsStore } from "../../../../core/spots/spots.store";
import { ReservationSpotItemsApi } from "../../../api/reservation-spot-items.api";
import {
  type ReservationDetail,
  type ReservationDetailSpotItem,
  ReservationState,
} from "../../../api/reservations.types";

type KeyRow = {
  readonly id: string;
  readonly spotLabel: string;
  readonly hasGivenKey: boolean;
};

@Component({
  selector: "kemp-is-reservation-summary",
  imports: [ButtonModule, TagModule, ToastModule],
  templateUrl: "./reservation-summary.html",
  styleUrl: "./reservation-summary.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class ReservationSummary {
  private readonly apiClient = inject(ApiClient);
  private readonly spotsStore = inject(SpotsStore);
  private readonly spotItemsApi = inject(ReservationSpotItemsApi);
  private readonly messages = inject(MessageService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly refresh = inject(RefreshController);

  readonly reservationId = input<string | null>(null);
  readonly editClicked = output<string>();
  readonly billClicked = output<string>();

  protected readonly canCreateBill = computed<boolean>(() =>
    (this.auth.currentUser()?.roles ?? []).includes(Roles.Receptionist)
  );

  protected onCreateBill(): void {
    const id = this.reservationId();
    if (!id) {
      return;
    }
    this.billClicked.emit(id);
    void this.router.navigate(["/staff/auth/desktop/bill/new"], {
      queryParams: { reservationId: id },
    });
  }

  protected readonly detail = httpResource<ReservationDetail>(() => {
    const id = this.reservationId();
    return id ? this.apiClient.url(`/reservations/${id}`) : undefined;
  });

  private readonly pendingKeyItemIds = signal<ReadonlySet<string>>(new Set());

  protected readonly guestName = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    if (!d) {
      return "";
    }
    const surname = d.reservationMakerSurname.trim();
    const name = d.reservationMakerName.trim();
    return [name, surname].filter(part => part.length > 0).join(" ");
  });

  protected readonly formattedRange = computed(() => {
    const d = this.detail.hasValue() ? this.detail.value() : null;
    return d ? `${this.formatDate(d.from)} – ${this.formatDate(d.to)}` : "";
  });

  // Key-handover UI is only visible while Confirmed; once CheckedIn the
  // section hides even if some spot items haven't been marked.
  protected readonly showKeySection = computed<boolean>(() => {
    return (
      this.detail.hasValue() &&
      this.detail.value().state === ReservationState.Confirmed
    );
  });

  protected readonly canCheckIn = computed<boolean>(
    () =>
      this.detail.hasValue() &&
      this.detail.value().state === ReservationState.Confirmed
  );

  private readonly checkingIn = signal<boolean>(false);
  protected readonly isCheckingIn = this.checkingIn.asReadonly();

  protected onCheckIn(): void {
    const id = this.reservationId();
    if (!id || this.checkingIn()) {
      return;
    }
    this.checkingIn.set(true);
    this.apiClient.post<void>(`/reservations/${id}/check-in`, null).subscribe({
      next: () => {
        this.checkingIn.set(false);
        this.detail.reload();
        this.refresh.refreshNow();
        this.messages.add({
          severity: "success",
          summary: "Rezervace",
          detail: "Rezervace ubytována.",
        });
      },
      error: () => {
        this.checkingIn.set(false);
        this.messages.add({
          severity: "error",
          summary: "Rezervace",
          detail: "Nepodařilo se ubytovat rezervaci.",
        });
      },
    });
  }

  protected readonly keyRows = computed<readonly KeyRow[]>(() => {
    if (!this.detail.hasValue()) {
      return [];
    }
    return this.detail.value().spotItems.map(item => this.toKeyRow(item));
  });

  protected isKeyPending(id: string): boolean {
    return this.pendingKeyItemIds().has(id);
  }

  protected onGiveKey(id: string): void {
    if (this.pendingKeyItemIds().has(id)) {
      return;
    }
    this.markKeyPending(id, true);
    this.spotItemsApi.giveKey(id).subscribe({
      next: () => {
        this.markKeyPending(id, false);
        this.detail.reload();
      },
      error: () => {
        this.markKeyPending(id, false);
        this.messages.add({
          severity: "error",
          summary: "Klíče",
          detail: "Nepodařilo se předat klíč.",
        });
      },
    });
  }

  protected onEdit(): void {
    const id = this.reservationId();
    if (id) {
      this.editClicked.emit(id);
    }
  }

  private toKeyRow(item: ReservationDetailSpotItem): KeyRow {
    const spotLabel =
      item.spotId === null
        ? "Nepřiřazená chata"
        : this.spotsStore.nameOf(item.spotId);
    return {
      id: item.id,
      spotLabel,
      hasGivenKey: item.hasGivenKey,
    };
  }

  private markKeyPending(id: string, pending: boolean): void {
    this.pendingKeyItemIds.update(set => {
      const next = new Set(set);
      if (pending) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  private formatDate(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
    if (!m) {
      return iso;
    }
    const [, , month, day] = m;
    return `${Number(day)}. ${Number(month)}.`;
  }
}
