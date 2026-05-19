import { HttpClient, httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";
import { TableModule } from "primeng/table";
import { ToastModule } from "primeng/toast";
import { finalize } from "rxjs";

import { formatClosedAt, formatCzk } from "./accounting-closures-data";
import { ApiClient } from "../../../core/api/api-client";
import type { User } from "../../../core/users/users.types";
import type { FinancialClosingSummary } from "../../api/financial-closings.types";

const YEAR_COUNT = 10;

@Component({
  selector: "kemp-is-accounting-closures",
  imports: [
    FormsModule,
    ButtonModule,
    MessageModule,
    SelectModule,
    TableModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: "./accounting-closures.page.html",
  styleUrl: "./accounting-closures.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingClosuresPage {
  private readonly apiClient = inject(ApiClient);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messages = inject(MessageService);
  private readonly router = inject(Router);

  protected onOpen(id: string): void {
    void this.router.navigate(["/staff/auth/desktop/financial-closings", id]);
  }

  protected readonly selectedYear = signal<number>(new Date().getFullYear());

  protected readonly downloadingId = signal<string | null>(null);

  protected readonly years: { label: string; value: number }[] = ((): {
    label: string;
    value: number;
  }[] => {
    const current = new Date().getFullYear();
    return Array.from({ length: YEAR_COUNT }, (_, i) => {
      const year = current - i;
      return { label: String(year), value: year };
    });
  })();

  protected readonly resource = httpResource<
    readonly FinancialClosingSummary[]
  >(() => {
    const y = this.selectedYear();
    return `${this.apiClient.url("/financial-closings")}?from=${y}-01-01&to=${y}-12-31`;
  });

  /** `includeDisabled=true` because closings reference users that may have been disabled later. */
  protected readonly usersResource = httpResource<readonly User[]>(() =>
    this.apiClient.url("/users?includeDisabled=true")
  );

  protected readonly usersById = computed<ReadonlyMap<string, string>>(() => {
    if (!this.usersResource.hasValue()) {
      return new Map();
    }
    return new Map(this.usersResource.value().map(u => [u.id, u.name]));
  });

  protected readonly rows = computed<FinancialClosingSummary[]>(() => {
    if (!this.resource.hasValue()) {
      return [];
    }
    return [...this.resource.value()];
  });

  protected readonly loading = computed(() => this.resource.isLoading());

  protected readonly hasError = computed(
    () => this.resource.error() !== undefined
  );

  protected readonly count = computed(() => this.rows().length);

  protected readonly totalAmount = computed(() =>
    this.rows().reduce((sum, c) => sum + c.totalAmount, 0)
  );

  protected onRetry(): void {
    this.resource.reload();
  }

  protected onDownload(id: string): void {
    if (this.downloadingId() !== null) {
      return;
    }
    this.downloadingId.set(id);
    this.http
      .get(this.apiClient.url(`/financial-closings/${id}/pdf`), {
        responseType: "blob",
      })
      .pipe(
        finalize(() => this.downloadingId.set(null)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          window.open(url, "_blank", "noopener");
          setTimeout(() => URL.revokeObjectURL(url), 60_000);
        },
        error: () => {
          this.messages.add({
            severity: "error",
            summary: "Chyba",
            detail: "Stažení PDF se nepodařilo.",
          });
        },
      });
  }

  protected displayedPerson(row: FinancialClosingSummary): string {
    if (row.createdByUserId === null) {
      return "—";
    }
    return this.usersById().get(row.createdByUserId) ?? "—";
  }

  protected readonly formatCzk = formatCzk;
  protected readonly formatClosedAt = formatClosedAt;
}
