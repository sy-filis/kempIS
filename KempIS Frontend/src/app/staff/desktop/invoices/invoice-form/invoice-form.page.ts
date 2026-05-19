import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  ViewEncapsulation,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";

import { MessageService } from "primeng/api";
import {
  type AutoCompleteCompleteEvent,
  AutoCompleteModule,
  type AutoCompleteSelectEvent,
} from "primeng/autocomplete";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";
import { SelectButtonModule } from "primeng/selectbutton";
import { ToastModule } from "primeng/toast";

import { InvoiceStatusHeader } from "./invoice-status-header/invoice-status-header";
import {
  AddressesApi,
  type AddressSuggestion,
} from "../../../../core/addresses/addresses.api";
import { ApiClient } from "../../../../core/api/api-client";
import { AuthService } from "../../../../core/auth/auth.service";
import { Roles } from "../../../../core/auth/roles";
import { NationalitiesStore } from "../../../../core/nationalities/nationalities.store";
import { ServicesStore } from "../../../../core/services/services.store";
import { InvoicesApi } from "../../../api/invoices.api";
import type {
  CreateInvoiceRequest,
  GetInvoiceItemView,
  GetInvoiceResponse,
  InvoiceItemInput,
} from "../../../api/invoices.types";
import { InvoiceStatus } from "../../../api/invoices.types";
import type { ReservationDetail } from "../../../api/reservations.types";
import { SERVICE_GROUP_LABELS } from "../../system-settings/shared/service-groups";
import type {
  CatalogueSpotGroup,
  VatRate,
} from "../../system-settings/shared/types";

const HOST_COUNTRY_ALPHA2 = "CZ";

const ADDRESS_WHISPERER_MIN_CHARS = 8;

/** Catalogue basePrice is treated as gross; net unitPrice is derived for the request body. */
type Row = {
  readonly id: string;
  readonly serviceGuid: string;
  readonly name: string;
  readonly quantity: number;
  readonly unitPriceGross: number;
  readonly vatRatePercentage: number;
};

type ServiceOption = {
  readonly id: string;
  readonly name: string;
  readonly groupLabel: string;
  readonly basePrice: number;
  readonly vatRatePercentage: number;
};

type DerivedReservationService = {
  readonly serviceId: string;
  readonly name: string;
  readonly groupLabel: string;
  readonly basePrice: number;
  readonly vatRatePercentage: number;
  readonly quantity: number;
};

type VatRecapRow = {
  readonly rate: number;
  readonly netBase: number;
  readonly vatAmount: number;
  readonly grossTotal: number;
};

type CountryOption = {
  readonly id: string;
  readonly label: string;
};

@Component({
  selector: "kemp-is-invoice-form",
  imports: [
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    InvoiceStatusHeader,
    MessageModule,
    SelectButtonModule,
    SelectModule,
    ToastModule,
  ],
  templateUrl: "./invoice-form.page.html",
  styleUrl: "./invoice-form.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  providers: [MessageService],
})
export class InvoiceFormPage {
  private readonly apiClient = inject(ApiClient);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly addressesApi = inject(AddressesApi);
  private readonly servicesStore = inject(ServicesStore);
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly reservationId = signal<string | null>(
    this.route.snapshot.queryParamMap.get("reservationId")
  );

  readonly invoiceId = input<string | undefined>();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.invoiceId() ? "edit" : "create"
  );

  protected readonly invoice = httpResource<GetInvoiceResponse>(() => {
    const id = this.invoiceId();
    return id
      ? this.apiClient.url(`/invoices/${encodeURIComponent(id)}`)
      : undefined;
  });

  /** Backend XORs payer / legalEntity and rejects payloads carrying both. */
  protected readonly payerType = signal<"physical" | "legal">("physical");

  protected readonly payerTypeOptions: {
    label: string;
    value: "physical" | "legal";
  }[] = [
    { label: "Fyzická osoba", value: "physical" },
    { label: "Právnická osoba", value: "legal" },
  ];

  protected readonly detail = httpResource<ReservationDetail>(() => {
    const id = this.reservationId();
    return id ? this.apiClient.url(`/reservations/${id}`) : undefined;
  });

  private readonly spotGroupsResource = httpResource<
    readonly CatalogueSpotGroup[]
  >(() => this.apiClient.url("/spot-groups"));

  private readonly vatRatesResource = httpResource<readonly VatRate[]>(() =>
    this.apiClient.url("/vat-rates")
  );

  private readonly vatRateById = computed<ReadonlyMap<string, number>>(() => {
    if (!this.vatRatesResource.hasValue()) {
      return new Map();
    }
    return new Map(this.vatRatesResource.value().map(r => [r.id, r.rate]));
  });

  protected readonly payerName = signal<string>("");
  protected readonly payerSurname = signal<string>("");
  protected readonly payerCountryId = signal<string | null>(null);
  protected readonly payerCity = signal<string>("");
  protected readonly payerZipCode = signal<string>("");
  protected readonly payerStreet = signal<string>("");
  protected readonly payerHouseNumber = signal<string>("");

  protected readonly leName = signal<string>("");
  protected readonly leCin = signal<string>("");
  protected readonly leTin = signal<string>("");
  protected readonly leCountryId = signal<string | null>(null);
  protected readonly leCity = signal<string>("");
  protected readonly leZipCode = signal<string>("");
  protected readonly leStreet = signal<string>("");
  protected readonly leHouseNumber = signal<string>("");

  protected readonly email = signal<string>("");
  protected readonly phoneNumber = signal<string>("");

  protected readonly aresLoading = signal<boolean>(false);
  protected readonly aresError = signal<string | null>(null);

  private readonly currentInvoice = computed<GetInvoiceResponse | null>(() =>
    this.invoice.hasValue() ? this.invoice.value() : null
  );

  protected readonly canEdit = computed<boolean>(() => {
    if (this.mode() === "create") {
      return true;
    }
    const inv = this.currentInvoice();
    if (!inv || inv.status !== InvoiceStatus.Draft) {
      return false;
    }
    const roles = this.auth.currentUser()?.roles ?? [];
    return (
      roles.includes(Roles.Receptionist) || roles.includes(Roles.Accountant)
    );
  });

  protected readonly headerTitle = computed<string>(() => {
    if (this.mode() === "create") {
      return "Nová faktura";
    }
    const inv = this.currentInvoice();
    return inv?.number ?? "Návrh faktury";
  });

  protected readonly addressSuggestions = signal<AddressSuggestion[]>([]);

  protected readonly rows = signal<readonly Row[]>([]);

  protected readonly submitting = signal<boolean>(false);
  protected readonly submitError = signal<string | null>(null);

  /** Guards against re-seeding on subsequent reservation reloads which would clobber edits. */
  private prefilled = false;

  protected readonly countryOptions = computed<CountryOption[]>(() => {
    const list = [...this.nationalitiesStore.all()];
    list.sort((a, b) => a.name.localeCompare(b.name, "cs"));
    return list.map(n => ({ id: n.id, label: n.name }));
  });

  private readonly hostCountryId = computed<string | null>(() => {
    const host = this.nationalitiesStore
      .all()
      .find(n => n.alpha2 === HOST_COUNTRY_ALPHA2);
    return host?.id ?? null;
  });

  protected readonly stayDays = computed<number>(() => {
    if (!this.detail.hasValue()) {
      return 1;
    }
    const d = this.detail.value();
    const from = new Date(d.from);
    const to = new Date(d.to);
    const ms = to.getTime() - from.getTime();
    if (Number.isNaN(ms) || ms <= 0) {
      return 1;
    }
    return Math.max(1, Math.round(ms / 86_400_000));
  });

  protected rowTotalGross(row: Row): number {
    return row.quantity * row.unitPriceGross;
  }

  protected rowNetBase(row: Row): number {
    return this.rowTotalGross(row) / (1 + row.vatRatePercentage / 100);
  }

  protected rowVatAmount(row: Row): number {
    return this.rowTotalGross(row) - this.rowNetBase(row);
  }

  protected readonly itemsNetTotal = computed<number>(() =>
    this.rows().reduce((sum, r) => sum + this.rowNetBase(r), 0)
  );

  protected readonly itemsVatTotal = computed<number>(() =>
    this.rows().reduce((sum, r) => sum + this.rowVatAmount(r), 0)
  );

  protected readonly grandTotal = computed<number>(() =>
    this.rows().reduce((sum, r) => sum + this.rowTotalGross(r), 0)
  );

  protected readonly vatRecap = computed<VatRecapRow[]>(() => {
    const buckets = new Map<
      number,
      { netBase: number; vatAmount: number; grossTotal: number }
    >();
    for (const r of this.rows()) {
      const bucket = buckets.get(r.vatRatePercentage) ?? {
        netBase: 0,
        vatAmount: 0,
        grossTotal: 0,
      };
      bucket.netBase += this.rowNetBase(r);
      bucket.vatAmount += this.rowVatAmount(r);
      bucket.grossTotal += this.rowTotalGross(r);
      buckets.set(r.vatRatePercentage, bucket);
    }
    return Array.from(buckets, ([rate, b]) => ({ rate, ...b })).sort(
      (a, b) => b.rate - a.rate
    );
  });

  protected readonly derivedServices = computed<DerivedReservationService[]>(
    () => {
      if (!this.detail.hasValue()) {
        return [];
      }
      const services = this.servicesStore.active();
      if (services.length === 0) {
        return [];
      }
      const spotGroups = this.spotGroupsResource.hasValue()
        ? this.spotGroupsResource.value()
        : [];
      const spotGroupServiceById = new Map<string, string>(
        spotGroups.map(g => [g.id, g.serviceId])
      );
      const serviceById = new Map(services.map(svc => [svc.id, svc]));

      const counts = new Map<string, number>();
      const bump = (serviceId: string, by: number): void => {
        if (by <= 0 || !serviceById.has(serviceId)) {
          return;
        }
        counts.set(serviceId, (counts.get(serviceId) ?? 0) + by);
      };

      const days = this.stayDays();
      const d = this.detail.value();
      for (const item of d.serviceItems) {
        bump(item.serviceId, item.quantity);
      }
      for (const spot of d.spotItems) {
        const serviceId = spotGroupServiceById.get(spot.spotGroupId);
        if (serviceId) {
          bump(serviceId, days);
        }
      }
      for (const v of d.vehicles) {
        if (v.serviceId) {
          bump(v.serviceId, days);
        }
      }

      const out: DerivedReservationService[] = [];
      const rateById = this.vatRateById();
      for (const [serviceId, quantity] of counts) {
        const svc = serviceById.get(serviceId);
        if (!svc) {
          continue;
        }
        out.push({
          serviceId,
          name: svc.name,
          groupLabel: SERVICE_GROUP_LABELS[svc.serviceGroup],
          basePrice: svc.basePrice,
          vatRatePercentage: rateById.get(svc.vatRateId) ?? 0,
          quantity,
        });
      }
      return out.sort((a, b) => a.name.localeCompare(b.name, "cs"));
    }
  );

  protected readonly usedServiceIds = computed<ReadonlySet<string>>(
    () => new Set(this.rows().map(r => r.serviceGuid))
  );

  protected readonly addableServices = computed<ServiceOption[]>(() => {
    const used = this.usedServiceIds();
    const fromReservation = new Set(
      this.derivedServices().map(s => s.serviceId)
    );
    const rateById = this.vatRateById();
    return this.servicesStore
      .active()
      .filter(svc => !used.has(svc.id) && !fromReservation.has(svc.id))
      .map<ServiceOption>(svc => ({
        id: svc.id,
        name: svc.name,
        groupLabel: SERVICE_GROUP_LABELS[svc.serviceGroup],
        basePrice: svc.basePrice,
        vatRatePercentage: rateById.get(svc.vatRateId) ?? 0,
      }))
      .sort((a, b) => a.name.localeCompare(b.name, "cs"));
  });

  constructor() {
    effect(() => {
      const host = this.hostCountryId();
      if (!host) {
        return;
      }
      if (this.payerCountryId() === null) {
        this.payerCountryId.set(host);
      }
      if (this.leCountryId() === null) {
        this.leCountryId.set(host);
      }
    });

    effect(() => {
      if (this.mode() !== "create") {
        return;
      }
      if (this.prefilled || !this.detail.hasValue()) {
        return;
      }
      if (
        this.servicesStore.active().length === 0 ||
        !this.spotGroupsResource.hasValue() ||
        !this.vatRatesResource.hasValue()
      ) {
        return;
      }
      this.prefilled = true;
      const d = this.detail.value();
      this.payerName.set(d.reservationMakerName);
      this.payerSurname.set(d.reservationMakerSurname);
      this.email.set(d.reservationMakerEmail);
      this.phoneNumber.set(d.reservationMakerPhone);

      const seeded: Row[] = this.derivedServices().map(svc => ({
        id: crypto.randomUUID() as string,
        serviceGuid: svc.serviceId,
        name: svc.name,
        quantity: svc.quantity,
        unitPriceGross: svc.basePrice,
        vatRatePercentage: svc.vatRatePercentage,
      }));
      if (seeded.length > 0) {
        this.rows.set(seeded);
      }
    });

    effect(() => {
      if (this.mode() !== "edit" || !this.invoice.hasValue()) {
        return;
      }
      if (!this.vatRatesResource.hasValue()) {
        return;
      }
      this.prefilled = true;
      const inv = this.invoice.value();

      const roles = this.auth.currentUser()?.roles ?? [];
      const showsReservation = roles.includes(Roles.Receptionist);
      if (showsReservation && this.reservationId() !== inv.reservationId) {
        this.reservationId.set(inv.reservationId);
      }

      if (inv.payer) {
        this.payerType.set("physical");
        this.payerName.set(inv.payer.name);
        this.payerSurname.set(inv.payer.surname);
        this.payerCountryId.set(inv.payer.address.countryId);
        this.payerCity.set(inv.payer.address.city);
        this.payerZipCode.set(inv.payer.address.zipCode);
        this.payerStreet.set(inv.payer.address.street);
        this.payerHouseNumber.set(inv.payer.address.houseNumber);
      } else if (inv.legalEntity) {
        this.payerType.set("legal");
        this.leName.set(inv.legalEntity.name);
        this.leCin.set(inv.legalEntity.cin);
        this.leTin.set(inv.legalEntity.tin);
        this.leCountryId.set(inv.legalEntity.address.countryId);
        this.leCity.set(inv.legalEntity.address.city);
        this.leZipCode.set(inv.legalEntity.address.zipCode);
        this.leStreet.set(inv.legalEntity.address.street);
        this.leHouseNumber.set(inv.legalEntity.address.houseNumber);
      }

      this.email.set(inv.email);
      this.phoneNumber.set(inv.phoneNumber);

      const seeded: Row[] = inv.items.map(itemViewToRow);
      this.rows.set(seeded);
    });
  }

  protected addDerivedService(svc: DerivedReservationService): void {
    if (this.usedServiceIds().has(svc.serviceId)) {
      return;
    }
    this.rows.update(list => [
      ...list,
      {
        id: crypto.randomUUID() as string,
        serviceGuid: svc.serviceId,
        name: svc.name,
        quantity: svc.quantity,
        unitPriceGross: svc.basePrice,
        vatRatePercentage: svc.vatRatePercentage,
      },
    ]);
  }

  protected addServiceRow(option: ServiceOption): void {
    this.rows.update(list => [
      ...list,
      {
        id: crypto.randomUUID() as string,
        serviceGuid: option.id,
        name: option.name,
        quantity: 1,
        unitPriceGross: option.basePrice,
        vatRatePercentage: option.vatRatePercentage,
      },
    ]);
  }

  protected onServicePicked(serviceId: string | null): void {
    if (!serviceId) {
      return;
    }
    const option = this.addableServices().find(o => o.id === serviceId);
    if (option) {
      this.addServiceRow(option);
    }
    this.servicePickerValue.set(null);
  }

  protected readonly servicePickerValue = signal<string | null>(null);

  protected onRowQuantityChange(rowId: string, value: number | null): void {
    const next = Math.max(0, value ?? 0);
    this.rows.update(list =>
      list.map(r => (r.id === rowId ? { ...r, quantity: next } : r))
    );
  }

  protected onRowUnitPriceChange(rowId: string, value: number | null): void {
    const next = Math.max(0, value ?? 0);
    this.rows.update(list =>
      list.map(r => (r.id === rowId ? { ...r, unitPriceGross: next } : r))
    );
  }

  protected removeRow(rowId: string): void {
    this.rows.update(list => list.filter(r => r.id !== rowId));
  }

  protected onAresLookup(): void {
    const cin = this.leCin().trim();
    if (cin.length !== 8 || !/^\d{8}$/.test(cin)) {
      this.aresError.set("Zadejte 8místné IČO.");
      return;
    }
    this.aresLoading.set(true);
    this.aresError.set(null);
    this.invoicesApi.fromAres(cin).subscribe({
      next: r => {
        this.aresLoading.set(false);
        this.leName.set(r.name);
        this.leCin.set(r.cin);
        this.leTin.set(r.tin ?? "");
        this.leCity.set(r.address.city);
        this.leZipCode.set(r.address.zipCode);
        this.leStreet.set(r.address.street ?? "");
        this.leHouseNumber.set(r.address.houseNumber);
        const matchedCountry = this.nationalitiesStore
          .all()
          .find(n => n.alpha2 === r.address.countryCode);
        if (matchedCountry) {
          this.leCountryId.set(matchedCountry.id);
        }
      },
      error: () => {
        this.aresLoading.set(false);
        this.aresError.set("Subjekt s tímto IČO nebyl nalezen.");
      },
    });
  }

  private resolveWhispererForeign(): boolean | null {
    const countryId =
      this.payerType() === "physical"
        ? this.payerCountryId()
        : this.leCountryId();
    if (!countryId) {
      return null;
    }
    const country = this.nationalitiesStore.byId().get(countryId);
    if (!country) {
      return null;
    }
    return country.alpha2 !== HOST_COUNTRY_ALPHA2;
  }

  protected onAddressQuery(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < ADDRESS_WHISPERER_MIN_CHARS) {
      this.addressSuggestions.set([]);
      return;
    }
    const foreign = this.resolveWhispererForeign();
    if (foreign === null) {
      this.addressSuggestions.set([]);
      return;
    }
    this.addressesApi.whisperer(query, foreign).subscribe({
      next: results => this.addressSuggestions.set([...results]),
      error: () => this.addressSuggestions.set([]),
    });
  }

  protected onPayerAddressPick(event: AutoCompleteSelectEvent): void {
    this.applyAddressSuggestion(event.value as AddressSuggestion, "physical");
  }

  protected onLeAddressPick(event: AutoCompleteSelectEvent): void {
    this.applyAddressSuggestion(event.value as AddressSuggestion, "legal");
  }

  private applyAddressSuggestion(
    s: AddressSuggestion,
    side: "physical" | "legal"
  ): void {
    if (side === "physical") {
      this.payerStreet.set(s.street);
      this.payerHouseNumber.set(s.houseNumber);
      this.payerZipCode.set(s.zipCode);
      this.payerCity.set(s.city);
    } else {
      this.leStreet.set(s.street);
      this.leHouseNumber.set(s.houseNumber);
      this.leZipCode.set(s.zipCode);
      this.leCity.set(s.city);
    }
    const matchedCountry = this.nationalitiesStore
      .all()
      .find(n => n.alpha2 === s.countryCode);
    if (matchedCountry) {
      if (side === "physical") {
        this.payerCountryId.set(matchedCountry.id);
      } else {
        this.leCountryId.set(matchedCountry.id);
      }
    }
  }

  protected readonly canSubmit = computed<boolean>(() => {
    if (this.submitting()) {
      return false;
    }
    if (!this.canEdit()) {
      return false;
    }
    return this.collectErrors().length === 0;
  });

  private collectErrors(): readonly string[] {
    const errs: string[] = [];
    if (this.mode() === "create" && !this.reservationId()) {
      errs.push("Faktura musí být svázána s rezervací.");
    }
    if (this.payerType() === "physical") {
      if (!this.payerName().trim()) {
        errs.push("Vyplňte jméno plátce.");
      }
      if (!this.payerSurname().trim()) {
        errs.push("Vyplňte příjmení plátce.");
      }
      if (!this.payerStreet().trim()) {
        errs.push("Vyplňte ulici plátce.");
      }
      if (!this.payerHouseNumber().trim()) {
        errs.push("Vyplňte č.p. plátce.");
      }
      if (!this.payerZipCode().trim()) {
        errs.push("Vyplňte PSČ plátce.");
      }
      if (!this.payerCity().trim()) {
        errs.push("Vyplňte město plátce.");
      }
      if (!this.payerCountryId()) {
        errs.push("Vyplňte zemi plátce.");
      }
    } else {
      if (!this.leName().trim()) {
        errs.push("Vyplňte název odběratele.");
      }
      if (!this.leCin().trim()) {
        errs.push("Vyplňte IČO odběratele.");
      }
      if (!this.leStreet().trim()) {
        errs.push("Vyplňte ulici odběratele.");
      }
      if (!this.leHouseNumber().trim()) {
        errs.push("Vyplňte č.p. odběratele.");
      }
      if (!this.leZipCode().trim()) {
        errs.push("Vyplňte PSČ odběratele.");
      }
      if (!this.leCity().trim()) {
        errs.push("Vyplňte město odběratele.");
      }
      if (!this.leCountryId()) {
        errs.push("Vyplňte zemi odběratele.");
      }
    }
    const email = this.email().trim();
    if (!email) {
      errs.push("Vyplňte e-mail.");
    } else if (!email.includes("@")) {
      errs.push("E-mail nemá platný formát.");
    }
    if (!this.phoneNumber().trim()) {
      errs.push("Vyplňte telefonní číslo.");
    }
    const rows = this.rows();
    if (rows.length === 0) {
      errs.push("Přidejte alespoň jednu položku.");
    }
    if (rows.some(r => r.quantity <= 0)) {
      errs.push("Počet u každé položky musí být větší než nula.");
    }
    if (rows.some(r => r.unitPriceGross <= 0)) {
      errs.push("Cena u každé položky musí být větší než nula.");
    }
    return errs;
  }

  protected onSubmit(): void {
    const errs = this.collectErrors();
    if (errs.length > 0) {
      this.submitError.set(errs[0] ?? null);
      return;
    }
    const reservationId =
      this.mode() === "edit"
        ? (this.currentInvoice()?.reservationId ?? null)
        : this.reservationId();
    if (!reservationId) {
      return;
    }
    const items: InvoiceItemInput[] = this.rows().map(r => ({
      serviceGuid: r.serviceGuid === "" ? null : r.serviceGuid,
      name: r.name,
      quantity: r.quantity,
      unitPrice: r.unitPriceGross / (1 + r.vatRatePercentage / 100),
      vatRatePercentage: r.vatRatePercentage,
    }));

    const body = this.buildBody(reservationId, items);
    if (!body) {
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);

    const id = this.invoiceId();
    const success = (): void => {
      this.submitting.set(false);
      this.messageService.add({
        severity: "success",
        summary: "Hotovo",
        detail:
          this.mode() === "create" ? "Faktura vytvořena" : "Faktura uložena",
      });
      if (this.mode() === "create") {
        void this.router.navigate(["/staff/auth/desktop/invoices"]);
      } else {
        this.prefilled = false;
        this.invoice.reload();
      }
    };
    const failure = (err: unknown): void => {
      this.submitting.set(false);
      const message =
        err instanceof Error
          ? err.message
          : this.mode() === "create"
            ? "Nepodařilo se vytvořit fakturu."
            : "Nepodařilo se uložit změny.";
      this.submitError.set(message);
    };

    if (this.mode() === "edit" && id) {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const { reservationId: _r, ...update } = body;
      this.invoicesApi.update(id, update).subscribe({
        next: success,
        error: failure,
      });
    } else {
      this.invoicesApi.create(body).subscribe({
        next: success,
        error: failure,
      });
    }
  }

  protected onCancel(): void {
    void this.router.navigate(["/staff/auth/desktop/invoices"]);
  }

  protected onReservationClick(reservationId: string): void {
    void this.router.navigate([
      "/staff/auth/desktop/reservations",
      reservationId,
      "edit",
    ]);
  }

  protected onInvoiceTransitioned(): void {
    this.invoice.reload();
  }

  private buildBody(
    reservationId: string,
    items: readonly InvoiceItemInput[]
  ): CreateInvoiceRequest | null {
    const email = this.email().trim();
    const phoneNumber = this.phoneNumber().trim();
    if (this.payerType() === "physical") {
      const country = this.payerCountryId();
      if (!country) {
        return null;
      }
      return {
        reservationId,
        payer: {
          name: this.payerName().trim(),
          surname: this.payerSurname().trim(),
          address: {
            countryId: country,
            city: this.payerCity().trim(),
            zipCode: this.payerZipCode().trim(),
            street: this.payerStreet().trim(),
            houseNumber: this.payerHouseNumber().trim(),
          },
        },
        email,
        phoneNumber,
        items: [...items],
      };
    }
    const country = this.leCountryId();
    if (!country) {
      return null;
    }
    return {
      reservationId,
      legalEntity: {
        name: this.leName().trim(),
        cin: this.leCin().trim(),
        tin: this.leTin().trim(),
        address: {
          countryId: country,
          city: this.leCity().trim(),
          zipCode: this.leZipCode().trim(),
          street: this.leStreet().trim(),
          houseNumber: this.leHouseNumber().trim(),
        },
      },
      email,
      phoneNumber,
      items: [...items],
    };
  }

  protected formatDateRange(fromIso: string, toIso: string): string {
    const f = new Date(fromIso);
    const t = new Date(toIso);
    const opts: Intl.DateTimeFormatOptions = {
      day: "numeric",
      month: "numeric",
      year: "numeric",
    };
    return `${f.toLocaleDateString("cs-CZ", opts)} - ${t.toLocaleDateString(
      "cs-CZ",
      opts
    )}`;
  }

  protected formatCzk(value: number): string {
    return value.toLocaleString("cs-CZ", {
      style: "currency",
      currency: "CZK",
      maximumFractionDigits: 0,
    });
  }
}

function itemViewToRow(it: GetInvoiceItemView): Row {
  const factor = 1 + it.vatRatePercentage / 100;
  return {
    id: it.id,
    serviceGuid: it.serviceGuid ?? "",
    name: it.name,
    quantity: it.quantity,
    unitPriceGross: it.unitPrice * factor,
    vatRatePercentage: it.vatRatePercentage,
  };
}
