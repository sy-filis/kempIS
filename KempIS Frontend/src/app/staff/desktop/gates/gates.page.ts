import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ConfirmationService, MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { IconFieldModule } from "primeng/iconfield";
import { InputIconModule } from "primeng/inputicon";
import { InputTextModule } from "primeng/inputtext";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";

import { CardFormDialog } from "./card-form-dialog/card-form-dialog";
import { AccessCardsApi } from "../../../core/access-cards/access-cards.api";
import type { AccessCard } from "../../../core/access-cards/access-cards.types";
import { ApiClient } from "../../../core/api/api-client";

@Component({
  selector: "kemp-is-gates",
  imports: [
    FormsModule,
    ButtonModule,
    CardFormDialog,
    ConfirmDialogModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    TableModule,
    TagModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: "./gates.page.html",
  styleUrl: "./gates.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GatesPage {
  private readonly apiClient = inject(ApiClient);
  private readonly api = inject(AccessCardsApi);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly cards = httpResource<readonly AccessCard[]>(() =>
    this.apiClient.url("/access-cards")
  );

  protected readonly uidFilter = signal<string>("");

  protected readonly rows = computed<AccessCard[]>(() => {
    if (!this.cards.hasValue()) {
      return [];
    }
    const all = this.cards.value();
    const filter = this.uidFilter().trim();
    if (filter.length === 0) {
      return [...all];
    }
    return all.filter(c => String(c.uid).includes(filter));
  });

  protected readonly loading = computed(() => this.cards.isLoading());

  protected readonly totalCount = computed(() =>
    this.cards.hasValue() ? this.cards.value().length : 0
  );

  protected readonly totalDeposit = computed(() =>
    this.cards.hasValue()
      ? this.cards.value().reduce((sum, c) => sum + c.deposit, 0)
      : 0
  );

  protected readonly formVisible = signal<boolean>(false);
  protected readonly editingCard = signal<AccessCard | null>(null);

  protected onCreate(): void {
    this.editingCard.set(null);
    this.formVisible.set(true);
  }

  protected onEdit(card: AccessCard): void {
    this.editingCard.set(card);
    this.formVisible.set(true);
  }

  protected onReturn(card: AccessCard): void {
    this.confirm.confirm({
      header: "Vrátit kartu",
      message: `Opravdu chcete vrátit kartu s UID „${card.uid}“? Vrácenou kartu nelze obnovit.`,
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Vrátit",
      rejectLabel: "Zrušit",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.api.returnCard(card.id).subscribe({
          next: () => {
            this.messages.add({
              severity: "success",
              summary: "Karta vrácena",
              detail: `UID ${card.uid}`,
            });
            this.cards.reload();
          },
          error: () => {
            this.messages.add({
              severity: "error",
              summary: "Chyba",
              detail: "Kartu se nepodařilo vrátit.",
            });
          },
        });
      },
    });
  }

  protected onFormSaved(message: string): void {
    this.formVisible.set(false);
    this.messages.add({
      severity: "success",
      summary: "Uloženo",
      detail: message,
    });
    this.cards.reload();
  }

  protected formatDeposit(value: number): string {
    return new Intl.NumberFormat("cs-CZ", {
      style: "currency",
      currency: "CZK",
      maximumFractionDigits: 2,
    }).format(value);
  }

  protected formatIssued(iso: string): string {
    return new Date(iso).toLocaleDateString("cs-CZ", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
  }

  protected formatValidUntil(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
    if (m) {
      return `${Number(m[3])}. ${Number(m[2])}. ${m[1]}`;
    }
    return iso;
  }

  /** `validUntil` is inclusive on the day itself. */
  protected isExpired(iso: string): boolean {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
    if (!m) {
      return false;
    }
    const end = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return end.getTime() < today.getTime();
  }
}
