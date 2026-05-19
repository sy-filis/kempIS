import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { DatePickerModule } from "primeng/datepicker";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import {
  computeStatus,
  documentTypeLabel,
  formatDob,
  formatStay,
  isCzechGuest,
  statusIcon,
  statusLabel,
  statusSeverity,
} from "./guests-data";
import { ApiClient } from "../../../core/api/api-client";
import { dateToIso } from "../../../shared/date-iso";
import type { Guest } from "../../api/guests.types";
import type { GuestDocumentType } from "../../api/reservations.types";
import {
  GuestEditorDialogComponent,
  type GuestEditorInput,
} from "../guest-editor-dialog/guest-editor-dialog.component";

const SEARCH_DEBOUNCE_MS = 250;

function startOfToday(): Date {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

@Component({
  selector: "kemp-is-guests",
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    GuestEditorDialogComponent,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageModule,
    TableModule,
    TagModule,
    ToastModule,
  ],
  templateUrl: "./guests.page.html",
  styleUrl: "./guests.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class GuestsPage {
  private readonly apiClient = inject(ApiClient);

  protected readonly from = signal<Date | null>(startOfToday());
  protected readonly to = signal<Date | null>(startOfToday());
  protected readonly search = signal<string>("");
  private readonly searchDebounced = signal<string>("");

  constructor() {
    effect(onCleanup => {
      const v = this.search();
      const id = setTimeout(
        () => this.searchDebounced.set(v),
        SEARCH_DEBOUNCE_MS
      );
      onCleanup(() => clearTimeout(id));
    });
  }

  protected readonly resource = httpResource<readonly Guest[]>(() => {
    const f = this.from();
    const t = this.to();
    if (!f || !t) {
      return undefined;
    }
    const params = new URLSearchParams({
      from: dateToIso(f),
      to: dateToIso(t),
    });
    const s = this.searchDebounced().trim();
    if (s.length > 0) {
      params.set("search", s);
    }
    return `${this.apiClient.url("/guests")}?${params.toString()}`;
  });

  protected readonly rows = computed<Guest[]>(() =>
    this.resource.hasValue() ? [...this.resource.value()] : []
  );

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly hasError = computed(
    () => this.resource.error() !== undefined
  );

  protected readonly count = computed(() => this.rows().length);

  protected readonly minToDate = computed(() => this.from() ?? undefined);

  protected readonly editorVisible = signal<boolean>(false);
  protected readonly editingGuest = signal<Guest | null>(null);

  protected readonly editorInput = computed<GuestEditorInput | null>(() => {
    const g = this.editingGuest();
    if (!g) {
      return null;
    }
    const hasFull =
      g.address !== undefined &&
      g.nationalityId !== undefined &&
      g.reasonOfStay !== undefined;
    return {
      id: g.id,
      reservationId: g.reservationId,
      billId: g.billId ?? null,
      firstName: g.firstName,
      lastName: g.lastName,
      paysRecreationFee: g.paysRecreationFee ?? null,
      full: hasFull
        ? {
            nationalityId: g.nationalityId as string,
            dateOfBirth: g.dateOfBirth,
            documentType: g.documentType as GuestDocumentType | null,
            documentNumber: g.documentNumber,
            address: g.address as NonNullable<Guest["address"]>,
            reasonOfStay: g.reasonOfStay as string,
            stayFrom: g.stayDateRange.from,
            stayTo: g.stayDateRange.to,
            visaNumber: g.visaNumber ?? null,
            note: g.note ?? null,
            scartation: g.scartation ?? null,
            checkInAt: g.checkInAt,
            checkOutAt: g.checkOutAt,
          }
        : null,
    };
  });

  protected onRetry(): void {
    this.resource.reload();
  }

  protected openGuest(guest: Guest): void {
    this.editingGuest.set(guest);
    this.editorVisible.set(true);
  }

  protected onEditorVisibleChange(visible: boolean): void {
    this.editorVisible.set(visible);
    if (!visible) {
      this.editingGuest.set(null);
    }
  }

  protected onGuestSaved(): void {
    this.editorVisible.set(false);
    this.editingGuest.set(null);
    this.resource.reload();
  }

  protected readonly computeStatus = computeStatus;
  protected readonly statusLabel = statusLabel;
  protected readonly statusSeverity = statusSeverity;
  protected readonly statusIcon = statusIcon;
  protected readonly documentTypeLabel = documentTypeLabel;
  protected readonly formatDob = formatDob;
  protected readonly formatStay = formatStay;
  protected readonly isCzechGuest = isCzechGuest;
}
