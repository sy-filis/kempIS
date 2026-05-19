import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from "@angular/core";
import { Router } from "@angular/router";

import { fmtCzk } from "./dashboard-data";
import { ApiClient } from "../../../core/api/api-client";
import { type BillSummary, PaymentType } from "../../api/bills.types";
import { formatStay } from "../bills/bills-data";

type MoneyView = {
  readonly totalAmount: number;
  readonly cashAmount: number;
  readonly cardAmount: number;
  readonly notClosedBills: readonly BillSummary[];
};

const EMPTY: MoneyView = {
  totalAmount: 0,
  cashAmount: 0,
  cardAmount: 0,
  notClosedBills: [],
};

@Component({
  selector: "kemp-is-dash-money",
  imports: [],
  templateUrl: "./dashboard-money.html",
  styleUrl: "./dashboard-money.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashMoneyPanel {
  private readonly apiClient = inject(ApiClient);
  private readonly router = inject(Router);

  protected readonly fmtCzk = fmtCzk;
  protected readonly formatStay = formatStay;

  private readonly openBills = httpResource<readonly BillSummary[]>(() =>
    this.apiClient.url("/bills?closed=false")
  );

  protected readonly money = computed<MoneyView>(() => {
    if (!this.openBills.hasValue()) {
      return EMPTY;
    }

    const notClosed = this.openBills
      .value()
      .filter(b => b.financialClosingId === null);
    let cashAmount = 0;
    let cardAmount = 0;
    for (const b of notClosed) {
      if (b.paymentType === PaymentType.Cash) {
        cashAmount += b.amount;
      } else {
        cardAmount += b.amount;
      }
    }

    return {
      totalAmount: cashAmount + cardAmount,
      cashAmount,
      cardAmount,
      notClosedBills: notClosed,
    };
  });

  protected onOpenBill(id: string): void {
    void this.router.navigate(["/staff/auth/desktop/bills", id]);
  }
}
