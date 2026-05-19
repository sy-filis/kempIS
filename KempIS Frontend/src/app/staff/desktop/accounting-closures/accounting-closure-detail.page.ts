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
import { ActivatedRoute, Router } from "@angular/router";

import { MessageService } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { MessageModule } from "primeng/message";
import { TableModule } from "primeng/table";
import { TagModule } from "primeng/tag";
import { ToastModule } from "primeng/toast";
import { concatMap, defer, from, lastValueFrom } from "rxjs";

import { formatClosedAt, formatCzk } from "./accounting-closures-data";
import { ApiClient } from "../../../core/api/api-client";
import { PrinterServerApi } from "../../../core/printing/printer-server.api";
import { PrinterSettingsStore } from "../../../core/printing/printer-settings.store";
import { FinancialClosingsApi } from "../../api/financial-closings.api";
import type {
  FinancialClosingBillRow,
  FinancialClosingDetail,
  FinancialClosingVatByServiceRow,
  FinancialClosingVatRecapRow,
} from "../../api/financial-closings.types";
import { PaymentType } from "../../api/reservations.types";

const CLOSING_TASK = "financial-closing" as const;

@Component({
  selector: "kemp-is-accounting-closure-detail",
  imports: [ButtonModule, MessageModule, TableModule, TagModule, ToastModule],
  templateUrl: "./accounting-closure-detail.page.html",
  styleUrl: "./accounting-closure-detail.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [MessageService],
})
export class AccountingClosureDetailPage {
  private readonly apiClient = inject(ApiClient);
  private readonly closingsApi = inject(FinancialClosingsApi);
  private readonly printerApi = inject(PrinterServerApi);
  private readonly printerStore = inject(PrinterSettingsStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messages = inject(MessageService);

  private readonly routeParams = toSignal(this.route.paramMap, {
    requireSync: true,
  });

  protected readonly closingId = computed<string>(
    () => this.routeParams().get("id") ?? ""
  );

  protected readonly viewing = signal<boolean>(false);
  protected readonly printing = signal<boolean>(false);

  protected readonly detailResource = httpResource<FinancialClosingDetail>(
    () => {
      const id = this.closingId();
      return id ? this.apiClient.url(`/financial-closings/${id}`) : undefined;
    }
  );

  protected readonly detail = computed<FinancialClosingDetail | null>(() =>
    this.detailResource.hasValue() ? this.detailResource.value() : null
  );

  /** p-table requires a mutable array; the detail's collections are readonly. */
  protected readonly vatRecap = computed<FinancialClosingVatRecapRow[]>(() => {
    const d = this.detail();
    return d === null ? [] : [...d.vatRecap];
  });

  protected readonly vatByService = computed<FinancialClosingVatByServiceRow[]>(
    () => {
      const d = this.detail();
      return d === null ? [] : [...d.vatRecapByServiceType];
    }
  );

  protected readonly bills = computed<FinancialClosingBillRow[]>(() => {
    const d = this.detail();
    return d === null ? [] : [...d.bills];
  });

  protected readonly loading = computed<boolean>(() =>
    this.detailResource.isLoading()
  );

  protected readonly hasError = computed<boolean>(
    () => this.detailResource.error() !== undefined
  );

  protected readonly defaultPrinter =
    this.printerStore.defaultFor(CLOSING_TASK);
  protected readonly defaultCopies = this.printerStore.copiesFor(CLOSING_TASK);
  protected readonly printerServerUrl = this.printerStore.serverUrl;

  protected readonly canPrint = computed<boolean>(() => {
    return (
      this.detail() !== null &&
      this.defaultPrinter() !== null &&
      this.printerServerUrl() !== ""
    );
  });

  protected readonly formatCzk = formatCzk;
  protected readonly formatClosedAt = formatClosedAt;

  protected billCount(): number {
    return this.detail()?.bills.length ?? 0;
  }

  protected onOpenBill(id: string): void {
    void this.router.navigate(["/staff/auth/desktop/bills", id]);
  }

  protected formatVatRate(rate: number): string {
    return `${rate.toLocaleString("cs-CZ")} %`;
  }

  protected paymentTypeLabel(type: number): string {
    switch (type) {
      case PaymentType.Cash:
        return "Hotově";
      case PaymentType.Card:
        return "Kartou";
      default:
        return "—";
    }
  }

  protected formatIssuedAt(iso: string): string {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) {
      return iso;
    }
    return d.toLocaleDateString("cs-CZ", {
      day: "numeric",
      month: "numeric",
      year: "numeric",
    });
  }

  protected onViewPdf(): void {
    const id = this.detail()?.id;
    if (!id || this.viewing()) {
      return;
    }
    this.viewing.set(true);
    this.closingsApi
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

  protected onPrint(): void {
    const id = this.detail()?.id;
    const printer = this.defaultPrinter();
    const server = this.printerServerUrl();
    if (!id || printer === null || server === "" || this.printing()) {
      return;
    }
    const copies = this.defaultCopies();
    this.printing.set(true);
    this.closingsApi
      .getPdf(id)
      .pipe(
        concatMap(blob =>
          defer(() => from(this.printNTimes(server, printer, blob, copies)))
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: () => {
          this.printing.set(false);
          this.messages.add({
            severity: "success",
            summary: "Tisk",
            detail:
              copies === 1
                ? "Závěrka odeslána na tiskárnu."
                : `Závěrka odeslána na tiskárnu (${copies}×).`,
          });
        },
        error: () => {
          this.printing.set(false);
          this.messages.add({
            severity: "error",
            summary: "Tisk",
            detail: "Tisk se nepodařil.",
          });
        },
      });
  }

  /** Print server has no `copies` parameter, so we serialize copies on the frontend. */
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
}
