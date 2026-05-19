export type FinancialClosingSummary = {
  id: string;
  financialClosingId: number;
  createdByUserId: string | null;
  closedAtUtc: string;
  totalAmount: number;
  billCount: number;
};

export type CreateFinancialClosingResponse = {
  id: string;
  financialClosingId: number;
  totalAmount: number;
  billCount: number;
};

/** `paymentType` mirrors `Domain.Finance.Payments.PaymentType`; `kind`
 *  mirrors `Domain.Finance.Bills.BillKind`. `originalBillId` references
 *  the bill this one corrects/refunds, when applicable. */
export type FinancialClosingBillRow = {
  id: string;
  number: string;
  issuedAtUtc: string;
  payerName: string;
  paymentType: number;
  total: number;
  kind: number;
  originalBillId: string | null;
};

export type FinancialClosingPaymentTotals = {
  cash: number;
  card: number;
  total: number;
};

export type FinancialClosingVatRecapRow = {
  vatRatePercentage: number;
  net: number;
  vat: number;
  gross: number;
};

export type FinancialClosingVatByServiceRow = {
  serviceTypeName: string;
  vatRatePercentage: number;
  net: number;
  vat: number;
  gross: number;
};

export type FinancialClosingDetail = {
  id: string;
  financialClosingId: number;
  closedAtUtc: string;
  createdByUserId: string | null;
  bills: readonly FinancialClosingBillRow[];
  paymentTotals: FinancialClosingPaymentTotals;
  vatRecap: readonly FinancialClosingVatRecapRow[];
  vatRecapByServiceType: readonly FinancialClosingVatByServiceRow[];
};
