import { LowerCasePipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import {
  type AutoCompleteCompleteEvent,
  AutoCompleteModule,
  type AutoCompleteSelectEvent,
} from "primeng/autocomplete";
import { ButtonModule } from "primeng/button";
import { CheckboxModule } from "primeng/checkbox";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";

import {
  AddressesApi,
  type AddressSuggestion,
} from "../../../../core/addresses/addresses.api";
import { LanguagesStore } from "../../../../core/languages/languages.store";
import { NationalitiesStore } from "../../../../core/nationalities/nationalities.store";
import { PRINT_TASK_LABELS } from "../../../../core/printing/print-task";
import { PrinterSettingsStore } from "../../../../core/printing/printer-settings.store";
import { PaymentType } from "../../../api/bills.types";
import { InvoicesApi } from "../../../api/invoices.api";
import type { Language, Nationality } from "../../system-settings/shared/types";
import type { LegalEntityForm, PayerForm } from "../bill-state";
import { BillState } from "../bill-state";

type PaymentMethodOption = {
  readonly type: PaymentType;
  readonly label: string;
  readonly icon: string;
  readonly hint: string;
};

const PAYMENT_METHOD_OPTIONS: readonly PaymentMethodOption[] = [
  {
    type: PaymentType.Cash,
    label: "Hotovost",
    icon: "pi-money-bill",
    hint: "Vystavit pokladní doklad",
  },
  {
    type: PaymentType.Card,
    label: "Platební karta",
    icon: "pi-credit-card",
    hint: "Zadejte částku do terminálu ručně",
  },
];

const CUSTOM_PAYER_ID = "__custom__";

type PayerOption = {
  readonly id: string;
  readonly label: string;
};

const ADDRESS_WHISPERER_MIN_CHARS = 8;
const HOST_COUNTRY_ALPHA2 = "CZ";

@Component({
  selector: "kemp-is-bill-step8-payment",
  imports: [
    FormsModule,
    LowerCasePipe,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
  ],
  templateUrl: "./step8-payment.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step8Payment {
  private readonly billState = inject(BillState);
  private readonly languagesStore = inject(LanguagesStore);
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly addressesApi = inject(AddressesApi);
  private readonly printerStore = inject(PrinterSettingsStore);

  protected readonly billPrinterLabel = PRINT_TASK_LABELS["bill"];
  protected readonly stickerPrinterLabel = PRINT_TASK_LABELS["tent-sticker"];

  protected readonly printBill = this.billState.printBill;
  protected readonly printBillCopies = this.billState.printBillCopies;
  protected readonly printTentStickers = this.billState.printTentStickers;
  protected readonly printTentStickerCopies =
    this.billState.printTentStickerCopies;

  protected readonly tentTotalQty = computed<number>(() =>
    this.billState.tents().reduce((sum, t) => sum + t.qty, 0)
  );

  protected readonly billPrinter = this.printerStore.defaultFor("bill");
  protected readonly stickerPrinter =
    this.printerStore.defaultFor("tent-sticker");
  protected readonly printServerUrl = this.printerStore.serverUrl;

  protected readonly addressSuggestions = signal<AddressSuggestion[]>([]);
  protected readonly minAddressChars = ADDRESS_WHISPERER_MIN_CHARS;

  protected readonly methods = PAYMENT_METHOD_OPTIONS;
  protected readonly customPayerId = CUSTOM_PAYER_ID;

  protected readonly paymentType = this.billState.paymentType;
  protected readonly payer = this.billState.payer;
  protected readonly payerSourceId = this.billState.payerSourceId;
  protected readonly languageId = this.billState.languageId;
  protected readonly legalEntity = this.billState.legalEntity;
  protected readonly legalEntityEnabled = this.billState.legalEntityEnabled;
  protected readonly grandTotal = this.billState.grandTotal;

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }

  protected readonly languages = computed<Language[]>(() => [
    ...this.languagesStore.all(),
  ]);

  protected readonly nationalityOptions = computed<Nationality[]>(() => [
    ...this.nationalitiesStore.all(),
  ]);

  protected readonly payerOptions = computed<PayerOption[]>(() => {
    const opts: PayerOption[] = [];
    for (const g of this.billState.preloadedGuests()) {
      if (!g.checked) {
        continue;
      }
      opts.push({ id: g.id, label: `${g.firstName} ${g.surname}` });
    }
    for (const g of this.billState.registryGuests()) {
      opts.push({ id: g.id, label: `${g.firstName} ${g.lastName}` });
    }
    opts.push({ id: CUSTOM_PAYER_ID, label: "Vlastní zadání" });
    return opts;
  });

  protected readonly aresLoading = signal<boolean>(false);
  protected readonly aresError = signal<string | null>(null);

  private printDefaultsSeeded = false;

  constructor() {
    if (!this.printDefaultsSeeded) {
      this.printDefaultsSeeded = true;
      this.printBillCopies.set(this.printerStore.copiesFor("bill")());
      this.printTentStickerCopies.set(this.tentTotalQty());
    }

    effect(() => {
      if (this.languageId() !== null) {
        return;
      }
      const langs = this.languages();
      if (langs.length === 0) {
        return;
      }
      const cs = langs.find(l => l.code.toLowerCase() === "cs");
      this.languageId.set((cs ?? langs[0])?.id ?? null);
    });

    effect(() => {
      if (this.payerSourceId() !== null) {
        return;
      }
      const first = this.payerOptions().find(o => o.id !== CUSTOM_PAYER_ID);
      if (first) {
        this.selectPayer(first.id);
      }
    });
  }

  protected selectPayer(id: string): void {
    this.payerSourceId.set(id);
    if (id === CUSTOM_PAYER_ID) {
      return;
    }
    const preloaded = this.billState.preloadedGuests().find(g => g.id === id);
    if (preloaded) {
      this.payer.set({
        name: preloaded.firstName,
        surname: preloaded.surname,
        street: preloaded.street,
        houseNumber: preloaded.houseNumber,
        zipCode: preloaded.postalCode,
        city: preloaded.city,
        countryCode: preloaded.country,
      });
      return;
    }
    const registry = this.billState.registryGuests().find(g => g.id === id);
    if (registry) {
      this.payer.set({
        name: registry.firstName,
        surname: registry.lastName,
        street: registry.address.street,
        houseNumber: registry.address.houseNumber,
        zipCode: registry.address.zipCode,
        city: registry.address.city,
        countryCode: registry.address.countryCode || "CZ",
      });
    }
  }

  protected updatePayer<K extends keyof PayerForm>(
    key: K,
    value: PayerForm[K]
  ): void {
    this.payer.update(p => ({ ...p, [key]: value }));
    // Manual edits switch the dropdown label off the guest source.
    if (this.payerSourceId() !== CUSTOM_PAYER_ID) {
      this.payerSourceId.set(CUSTOM_PAYER_ID);
    }
  }

  protected toggleLegalEntity(enabled: boolean): void {
    this.billState.setLegalEntityEnabled(enabled);
    if (!enabled) {
      this.aresError.set(null);
    }
  }

  protected updateLegalEntity<K extends keyof LegalEntityForm>(
    key: K,
    value: LegalEntityForm[K]
  ): void {
    this.legalEntity.update(le => (le === null ? le : { ...le, [key]: value }));
  }

  protected onAresLookup(): void {
    const le = this.legalEntity();
    if (le === null) {
      return;
    }
    const cin = le.cin.trim();
    if (cin.length !== 8 || !/^\d{8}$/.test(cin)) {
      this.aresError.set("Zadejte 8místné IČO.");
      return;
    }
    this.aresLoading.set(true);
    this.aresError.set(null);
    this.invoicesApi.fromAres(cin).subscribe({
      next: response => {
        this.aresLoading.set(false);
        this.legalEntity.update(current =>
          current === null
            ? current
            : {
                ...current,
                name: response.name,
                cin: response.cin,
                tin: response.tin ?? "",
                street: response.address.street ?? "",
                houseNumber: response.address.houseNumber,
                zipCode: response.address.zipCode,
                city: response.address.city,
                countryCode:
                  response.address.countryCode || current.countryCode,
              }
        );
      },
      error: () => {
        this.aresLoading.set(false);
        this.aresError.set("Subjekt s tímto IČO nebyl nalezen.");
      },
    });
  }

  private foreignFor(side: "physical" | "legal"): boolean {
    const code =
      side === "physical"
        ? this.payer().countryCode
        : (this.legalEntity()?.countryCode ?? HOST_COUNTRY_ALPHA2);
    return code !== HOST_COUNTRY_ALPHA2;
  }

  protected onPayerAddressQuery(event: AutoCompleteCompleteEvent): void {
    this.queryAddresses(event.query, "physical");
  }

  protected onLegalAddressQuery(event: AutoCompleteCompleteEvent): void {
    this.queryAddresses(event.query, "legal");
  }

  private queryAddresses(raw: string, side: "physical" | "legal"): void {
    const query = raw.trim();
    if (query.length < ADDRESS_WHISPERER_MIN_CHARS) {
      this.addressSuggestions.set([]);
      return;
    }
    this.addressesApi.whisperer(query, this.foreignFor(side)).subscribe({
      next: results => this.addressSuggestions.set([...results]),
      error: () => this.addressSuggestions.set([]),
    });
  }

  protected onPayerAddressPick(event: AutoCompleteSelectEvent): void {
    const s = event.value as AddressSuggestion;
    this.payer.set({
      ...this.payer(),
      street: s.street,
      houseNumber: s.houseNumber,
      zipCode: s.zipCode,
      city: s.city,
      countryCode: s.countryCode || this.payer().countryCode,
    });
    if (this.payerSourceId() !== CUSTOM_PAYER_ID) {
      this.payerSourceId.set(CUSTOM_PAYER_ID);
    }
  }

  protected onLegalAddressPick(event: AutoCompleteSelectEvent): void {
    const s = event.value as AddressSuggestion;
    this.legalEntity.update(le =>
      le === null
        ? le
        : {
            ...le,
            street: s.street,
            houseNumber: s.houseNumber,
            zipCode: s.zipCode,
            city: s.city,
            countryCode: s.countryCode || le.countryCode,
          }
    );
  }

  // Without these write-throughs the autocomplete echoes its own
  // buffer and the form state lags behind the typed value.
  protected onPayerStreetType(value: string | AddressSuggestion): void {
    if (typeof value === "string") {
      this.updatePayer("street", value);
    }
  }

  protected onLegalStreetType(value: string | AddressSuggestion): void {
    if (typeof value === "string") {
      this.updateLegalEntity("street", value);
    }
  }

  protected updatePrintBillCopies(value: number | null): void {
    this.printBillCopies.set(Math.max(1, value ?? 1));
  }

  protected updatePrintTentStickerCopies(value: number | null): void {
    this.printTentStickerCopies.set(Math.max(1, value ?? 1));
  }
}
