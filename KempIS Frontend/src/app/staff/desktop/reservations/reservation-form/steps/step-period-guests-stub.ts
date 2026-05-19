import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  type ElementRef,
  inject,
  input,
  model,
  output,
  signal,
  viewChild,
  ViewEncapsulation,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import {
  type AutoCompleteCompleteEvent,
  AutoCompleteModule,
  type AutoCompleteSelectEvent,
} from "primeng/autocomplete";
import { ButtonModule } from "primeng/button";
import { CheckboxModule } from "primeng/checkbox";
import { DatePickerModule } from "primeng/datepicker";
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";
import { TextareaModule } from "primeng/textarea";

import {
  AddressesApi,
  type AddressSuggestion,
} from "../../../../../core/addresses/addresses.api";
import type { EdokladyDraft } from "../../../../../core/edoklady/edoklady-attributes";
import { NationalitiesStore } from "../../../../../core/nationalities/nationalities.store";
import { dateToIso } from "../../../../../shared/date-iso";
import { type GuestRequest, GuestsApi } from "../../../../api/guests.api";
import type {
  GuestDocumentType,
  ReservationAddress,
  ReservationDetailGuest,
} from "../../../../api/reservations.types";
import type { Nationality } from "../../../system-settings/shared/types";
import {
  DocumentType,
  type GuestAddress,
  type RegistryGuest,
} from "../reservation-form-stub-data";
import { EdokladyTrigger } from "./edoklady-trigger/edoklady-trigger";

type DocumentTypeOption = {
  readonly value: DocumentType;
  readonly label: string;
};

const DOCUMENT_TYPE_LABELS: Record<DocumentType, string> = {
  [DocumentType.Passport]: "Cestovní pas",
  [DocumentType.IdCard]: "Občanský průkaz",
  [DocumentType.CzechResidencePermit]: "Povolení k pobytu v ČR",
  [DocumentType.LostPassportConfirmation]: "Náhradní cestovní doklad",
  [DocumentType.CzechDiplomatCard]: "Diplomatický průkaz ČR",
  [DocumentType.ChildInParentPassport]: "Dítě zapsané v pasu rodiče",
};

const HOST_COUNTRY_ALPHA2 = "CZ";

const CZ_OR_EU_TYPES: readonly DocumentType[] = [
  DocumentType.Passport,
  DocumentType.IdCard,
];

const NON_EU_TYPES: readonly DocumentType[] = [
  DocumentType.Passport,
  DocumentType.CzechResidencePermit,
  DocumentType.LostPassportConfirmation,
  DocumentType.CzechDiplomatCard,
  DocumentType.ChildInParentPassport,
];

function documentTypesForNationality(
  n: Pick<Nationality, "alpha2" | "isEu"> | undefined
): readonly DocumentType[] {
  if (!n) {
    return [];
  }
  if (n.alpha2 === HOST_COUNTRY_ALPHA2 || n.isEu) {
    return CZ_OR_EU_TYPES;
  }
  return NON_EU_TYPES;
}

const ADULT_AGE_YEARS = 15;
const FEE_ADULT_AGE_YEARS = 18;

// Wire sentinel used by the public check-in to mark biometric passports
// in the otherwise free-text VisaNumber column.
const BIOMETRIC_VISA_SENTINEL = "BIOMETRIKA";

// Short queries return too many matches ("Pra" matches every Prague street);
// 8 chars narrows it down. Also set as [minLength] on the p-autocomplete.
const ADDRESS_WHISPERER_MIN_CHARS = 8;

function ageOnDate(birth: Date | null, today: Date): number | null {
  if (!birth) {
    return null;
  }
  let age = today.getFullYear() - birth.getFullYear();
  const monthDiff = today.getMonth() - birth.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
    age -= 1;
  }
  return Math.max(0, age);
}

type QuickAddDraft = {
  lastName: string;
  firstName: string;
  documentNumber: string;
  birth: Date | null;
};

const EMPTY_QUICK_ADD: QuickAddDraft = {
  lastName: "",
  firstName: "",
  documentNumber: "",
  birth: null,
};

type RegistryDraft = {
  firstName: string;
  lastName: string;
  nationalityId: string;
  birth: Date | null;
  documentType: DocumentType | null;
  documentNumber: string;
  // Only meaningful for passports of nationalities granting visa-free
  // travel exclusively to biometric-passport holders.
  biometric: boolean;
  visaNumber: string;
  address: {
    street: string;
    houseNumber: string;
    zipCode: string;
    city: string;
    countryCode: string;
  };
  note: string;
};

const EMPTY_REGISTRY_DRAFT: RegistryDraft = {
  firstName: "",
  lastName: "",
  nationalityId: "",
  birth: null,
  documentType: null,
  documentNumber: "",
  biometric: false,
  visaNumber: "",
  address: {
    street: "",
    houseNumber: "",
    zipCode: "",
    city: "",
    countryCode: "",
  },
  note: "",
};

@Component({
  selector: "kemp-is-reservation-step-period-guests-stub",
  imports: [
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TextareaModule,
    EdokladyTrigger,
  ],
  templateUrl: "./step-period-guests-stub.html",
  styleUrl: "./step-period-guests-stub.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class StepPeriodGuestsStub {
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly addressesApi = inject(AddressesApi);
  private readonly guestsApi = inject(GuestsApi);

  readonly reservationId = input<string | null>(null);
  readonly guests = input<readonly ReservationDetailGuest[]>([]);
  readonly reservationFrom = input<Date | null>(null);
  readonly reservationTo = input<Date | null>(null);

  // Bill flow flips this on; reservations flow leaves it off.
  readonly showsFeeColumn = input<boolean>(false);

  // In stub mode (reservationId === null) the parent pipes guest state
  // through these models so rows survive switching wizard steps.
  readonly localGuests = model<readonly RegistryGuest[]>([]);
  readonly localFeePayingIds = model<ReadonlySet<string>>(new Set());

  readonly mutated = output<void>();
  readonly feePayingCountChanged = output<number>();

  protected readonly feePayingIds = signal<ReadonlySet<string>>(new Set());

  protected readonly feePaying = (id: string): boolean =>
    this.feePayingIds().has(id);

  protected toggleFeePaying(id: string): void {
    const target =
      this.reservationId() === null
        ? this.localFeePayingIds
        : this.feePayingIds;
    target.update(set => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected readonly saving = signal<boolean>(false);

  protected readonly nationalities = computed<Nationality[]>(() => [
    ...this.nationalitiesStore.all(),
  ]);

  protected readonly addressSuggestions = signal<AddressSuggestion[]>([]);

  protected readonly registryGuests = signal<readonly RegistryGuest[]>([]);

  constructor() {
    effect(() => {
      const id = this.reservationId();
      if (id !== null) {
        const list = this.guests();
        const byId = this.nationalitiesStore.byId();
        this.registryGuests.set(
          list.map(g => detailGuestToRegistryGuest(g, byId))
        );
        return;
      }
      this.registryGuests.set([...this.localGuests()]);
      this.feePayingIds.set(new Set(this.localFeePayingIds()));
    });

    effect(() => {
      this.feePayingCountChanged.emit(this.feePayingIds().size);
    });
  }

  protected readonly quickAdd = signal<QuickAddDraft>({
    ...EMPTY_QUICK_ADD,
  });

  protected readonly quickAddAddress = signal<AddressSuggestion | null>(null);

  protected readonly quickAddAddressInput = signal<string>("");

  protected readonly quickAddValid = computed<boolean>(
    () =>
      this.quickAdd().firstName.trim().length > 0 &&
      this.quickAdd().lastName.trim().length > 0
  );

  // PrimeNG dialog focuses the first focusable element (scanner input)
  // by default, but reception usually starts typing the surname.
  private readonly lastNameInput =
    viewChild<ElementRef<HTMLInputElement>>("lastNameInput");

  protected readonly tableScanInput = signal<string>("");
  protected readonly tableScanError = signal<string | null>(null);

  protected readonly scanInput = signal<string>("");
  protected readonly scanError = signal<string | null>(null);

  protected readonly adultAgeYears = ADULT_AGE_YEARS;

  private readonly today = new Date();

  protected readonly selectedNationality = computed<Nationality | undefined>(
    () => this.nationalitiesStore.byId().get(this.registryDraft().nationalityId)
  );

  protected readonly age = computed<number | null>(() =>
    ageOnDate(this.registryDraft().birth, this.today)
  );

  // Children under 15 from the host country don't have to present a document.
  protected readonly documentOptional = computed<boolean>(() => {
    const n = this.selectedNationality();
    return (
      n?.alpha2 === HOST_COUNTRY_ALPHA2 &&
      (this.age() ?? Number.POSITIVE_INFINITY) < ADULT_AGE_YEARS
    );
  });

  protected readonly showDocumentPicker = computed<boolean>(
    () => this.registryDraft().nationalityId !== ""
  );

  protected readonly allowedDocumentTypes = computed<readonly DocumentType[]>(
    () => documentTypesForNationality(this.selectedNationality())
  );

  protected readonly documentTypeOptions = computed<DocumentTypeOption[]>(() =>
    this.allowedDocumentTypes().map(value => ({
      value,
      label: DOCUMENT_TYPE_LABELS[value],
    }))
  );

  protected readonly isPassport = computed<boolean>(
    () => this.registryDraft().documentType === DocumentType.Passport
  );

  // Visible only for passports of nationalities whose visa-free travel
  // hinges on biometric data (e.g. Albania, Bosnia).
  protected readonly showBiometric = computed<boolean>(
    () =>
      this.isPassport() &&
      this.selectedNationality()?.biometricsRequired === true
  );

  protected readonly requiresVisa = computed<boolean>(() => {
    if (!this.isPassport() || this.registryDraft().biometric) {
      return false;
    }
    const n = this.selectedNationality();
    return n?.visaRequired === true || n?.biometricsRequired === true;
  });

  protected readonly documentNumberLabel = computed<string>(() =>
    this.isPassport() ? "Číslo pasu" : "Číslo dokladu"
  );

  protected readonly registryEditorOpen = signal<boolean>(false);
  protected readonly editingRegistryId = signal<string | null>(null);
  protected readonly registryDraft = signal<RegistryDraft>(emptyDraft());
  protected readonly registryDraftValid = computed(
    () =>
      this.registryDraft().firstName.trim().length > 0 &&
      this.registryDraft().lastName.trim().length > 0
  );

  protected nationalityAlpha3(id: string): string {
    return this.nationalitiesStore.alpha3Of(id);
  }

  protected addressLine(a: GuestAddress): string {
    const street = [a.street, a.houseNumber].filter(Boolean).join(" ").trim();
    const cityLine = [a.zipCode, a.city].filter(Boolean).join(" ").trim();
    return [street, cityLine].filter(Boolean).join(", ");
  }

  protected openNewRegistryEditor(): void {
    this.editingRegistryId.set(null);
    this.registryDraft.set(this.draftForNewGuest());
    this.scanInput.set("");
    this.scanError.set(null);
    this.registryEditorOpen.set(true);
  }

  // Inherit the previous guest's address - most camp guests arrive as
  // family groups sharing one. Walk from the end so the most recent wins.
  private draftForNewGuest(): RegistryDraft {
    const base = emptyDraft();
    const previous = this.lastGuestWithAddress();
    if (previous) {
      base.address = { ...previous.address };
    }
    return base;
  }

  private lastGuestWithAddress(): RegistryGuest | undefined {
    const list = this.registryGuests();
    for (let i = list.length - 1; i >= 0; i--) {
      const g = list[i];
      if (g && hasAddressContent(g.address)) {
        return g;
      }
    }
    return undefined;
  }

  protected readonly inheritedAddressLine = computed<string>(() => {
    const previous = this.lastGuestWithAddress();
    return previous ? this.addressLine(previous.address) : "";
  });

  protected readonly quickAddAddressPlaceholder = computed<string>(() => {
    if (this.quickAddAddress() !== null) {
      return "";
    }
    const inherited = this.inheritedAddressLine();
    return inherited ? `Vyhledat (jinak: ${inherited})` : "Vyhledat adresu";
  });

  // Scanner emits LASTNAME\tFIRSTNAME\tNATIONALITY(alpha3)\tDOC\tBIRTH
  // terminated by Enter. Browsers treat Tab as a focus move, so capture
  // it and inject a literal \t. Parse on Enter / blur.
  protected onScanKeyDown(event: KeyboardEvent): void {
    if (event.key === "Tab") {
      event.preventDefault();
      const input = event.target as HTMLInputElement;
      const pos = input.selectionStart ?? input.value.length;
      input.value = input.value.slice(0, pos) + "\t" + input.value.slice(pos);
      input.selectionStart = input.selectionEnd = pos + 1;
      input.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      this.tryParseScan();
    }
  }

  protected onScanInputChange(value: string): void {
    this.scanInput.set(value);
    this.scanError.set(null);
  }

  protected onScanBlur(): void {
    if (this.scanInput().includes("\t")) {
      this.tryParseScan();
    }
  }

  private tryParseScan(): void {
    const value = this.scanInput();
    if (!value.includes("\t")) {
      return;
    }
    const parts = value.split("\t");
    if (parts.length < 5) {
      this.scanError.set(
        "Skener vrátil neplatný formát (očekáváno 5 polí oddělených tabulátorem)."
      );
      this.scanInput.set("");
      return;
    }

    const [lastName, firstName, alpha3Raw, documentNumber, birthRaw] =
      parts as [string, string, string, string, string];
    const alpha3 = alpha3Raw.trim().toUpperCase();
    const nationality = this.nationalitiesStore
      .all()
      .find(n => n.alpha3 === alpha3);
    if (!nationality) {
      this.scanError.set(`Občanství „${alpha3}" nebylo v číselníku nalezeno.`);
      this.scanInput.set("");
      return;
    }

    const birth = parseScannerDate(birthRaw.trim());
    if (!birth) {
      this.scanError.set(
        `Datum narození „${birthRaw}" se nepodařilo rozpoznat.`
      );
      this.scanInput.set("");
      return;
    }

    this.registryDraft.update(d => ({
      ...d,
      lastName: toTitleCase(lastName.trim()),
      firstName: toTitleCase(firstName.trim()),
      nationalityId: nationality.id,
      birth,
      documentType: DocumentType.IdCard,
      documentNumber: documentNumber.trim(),
      biometric: false,
      visaNumber: "",
      address: {
        ...d.address,
        countryCode:
          d.address.countryCode === ""
            ? nationality.alpha2
            : d.address.countryCode,
      },
    }));
    this.scanInput.set("");
    this.scanError.set(null);
  }

  // Address fields use non-empty-wins merge so empty values from the
  // mapper (notably zipCode, which eDoklady mID does not supply) don't
  // clobber an inherited address from the previous family member.
  protected onEdokladyPresented(draft: EdokladyDraft): void {
    this.editingRegistryId.set(null);
    const base = this.draftForNewGuest();
    this.registryDraft.set({
      ...base,
      firstName: draft.firstName,
      lastName: draft.lastName,
      nationalityId: draft.nationalityId,
      birth: draft.birth,
      documentType: draft.documentType,
      documentNumber: draft.documentNumber,
      biometric: false,
      visaNumber: "",
      address: {
        street: draft.address.street || base.address.street,
        houseNumber: draft.address.houseNumber || base.address.houseNumber,
        zipCode: draft.address.zipCode || base.address.zipCode,
        city: draft.address.city || base.address.city,
        countryCode: draft.address.countryCode || base.address.countryCode,
      },
    });
    this.scanInput.set("");
    this.scanError.set(null);
    this.registryEditorOpen.set(true);
  }

  protected openRegistryEditor(g: RegistryGuest): void {
    this.editingRegistryId.set(g.id);
    const isBiometric = g.visaNumber === BIOMETRIC_VISA_SENTINEL;
    this.registryDraft.set({
      firstName: g.firstName,
      lastName: g.lastName,
      nationalityId: g.nationalityId,
      birth: parseBirthDate(g.birth),
      documentType: g.documentType,
      documentNumber: g.documentNumber ?? "",
      biometric: isBiometric,
      visaNumber: isBiometric ? "" : (g.visaNumber ?? ""),
      address: { ...g.address },
      note: g.note ?? "",
    });
    this.registryEditorOpen.set(true);
  }

  protected updateNationalityId(id: string): void {
    this.registryDraft.update(d => {
      const nationality = this.nationalitiesStore.byId().get(id);
      const allowed = documentTypesForNationality(nationality);
      const docStillAllowed =
        d.documentType !== null && allowed.includes(d.documentType);
      const countryCode =
        d.address.countryCode === "" && nationality
          ? nationality.alpha2
          : d.address.countryCode;
      return {
        ...d,
        nationalityId: id,
        documentType: docStillAllowed ? d.documentType : null,
        biometric: docStillAllowed ? d.biometric : false,
        visaNumber: docStillAllowed ? d.visaNumber : "",
        address: { ...d.address, countryCode },
      };
    });
  }

  protected updateDocumentType(value: DocumentType | null): void {
    this.registryDraft.update(d => {
      const stillPassport = value === DocumentType.Passport;
      return {
        ...d,
        documentType: value,
        biometric: stillPassport ? d.biometric : false,
        visaNumber: stillPassport ? d.visaNumber : "",
      };
    });
  }

  protected updateRegistryDraft<K extends keyof RegistryDraft>(
    key: K,
    value: RegistryDraft[K]
  ): void {
    this.registryDraft.update(d => ({ ...d, [key]: value }));
  }

  protected updateRegistryAddress<K extends keyof RegistryDraft["address"]>(
    key: K,
    value: RegistryDraft["address"][K]
  ): void {
    this.registryDraft.update(d => ({
      ...d,
      address: { ...d.address, [key]: value },
    }));
  }

  protected saveRegistryEdit(): void {
    if (!this.registryDraftValid() || this.saving()) {
      return;
    }
    const reservationId = this.reservationId();
    if (!reservationId) {
      const editing = this.editingRegistryId();
      const guest = this.buildRegistryGuestFromDraft(
        editing ?? crypto.randomUUID()
      );
      this.localGuests.update(list =>
        editing === null
          ? [...list, guest]
          : list.map(g => (g.id === editing ? guest : g))
      );
      if (editing === null && this.showsFeeColumn()) {
        const age = ageOnDate(this.registryDraft().birth, this.today);
        if (age === null || age >= FEE_ADULT_AGE_YEARS) {
          this.localFeePayingIds.update(set => {
            const next = new Set(set);
            next.add(guest.id);
            return next;
          });
        }
      }
      this.cancelRegistryEdit();
      return;
    }
    const request = this.buildGuestRequestFromDraft(reservationId);
    if (!request) {
      return;
    }
    const id = this.editingRegistryId();
    const onSuccess = (): void => {
      this.saving.set(false);
      this.cancelRegistryEdit();
      this.mutated.emit();
    };
    const onError = (): void => {
      this.saving.set(false);
    };

    this.saving.set(true);
    if (id === null) {
      this.guestsApi
        .create(request)
        .subscribe({ next: onSuccess, error: onError });
    } else {
      this.guestsApi
        .update(id, request)
        .subscribe({ next: onSuccess, error: onError });
    }
  }

  private buildRegistryGuestFromDraft(id: string): RegistryGuest {
    const d = this.registryDraft();
    return {
      id,
      firstName: d.firstName.trim(),
      lastName: d.lastName.trim(),
      nationalityId: d.nationalityId,
      birth: d.birth ? formatBirthDate(d.birth) : "",
      documentType: d.documentType,
      documentNumber: d.documentNumber.trim() || null,
      visaNumber: d.visaNumber.trim() || null,
      address: { ...d.address },
      note: d.note.trim() || null,
    };
  }

  // Returns null if required data is missing (e.g. birth date, which the
  // backend stores as DateOnly but the dialog's picker can be empty).
  private buildGuestRequestFromDraft(
    reservationId: string
  ): GuestRequest | null {
    const d = this.registryDraft();
    if (!d.birth) {
      return null;
    }
    const countryId = this.alpha2ToNationalityId(d.address.countryCode);
    if (!countryId) {
      return null;
    }
    const documentType =
      d.documentType ?? (DocumentType.IdCard as unknown as GuestDocumentType);

    const address: ReservationAddress = {
      countryId,
      street: d.address.street.trim(),
      houseNumber: d.address.houseNumber.trim(),
      zipCode: d.address.zipCode.trim(),
      city: d.address.city.trim(),
    };

    const reservationFrom = this.reservationFrom();
    const reservationTo = this.reservationTo();

    return {
      reservationId,
      billId: null,
      paysRecreationFee: null,
      firstName: d.firstName.trim(),
      lastName: d.lastName.trim(),
      nationalityId: d.nationalityId,
      dateOfBirth: dateToIso(d.birth),
      documentType: documentType as unknown as GuestDocumentType,
      documentNumber: d.documentNumber.trim(),
      address,
      // ReasonOfStay / StayDateRange aren't surfaced on the editor;
      // fall back to the reservation's own period when present.
      reasonOfStay: "",
      stayDateRange: {
        from: reservationFrom ? dateToIso(reservationFrom) : "",
        to: reservationTo ? dateToIso(reservationTo) : "",
      },
      visaNumber: this.serializeVisa(d),
      note: nullableTrim(d.note),
      scartation: null,
      checkInAt: null,
      checkOutAt: null,
      signaturePngBase64: null,
    };
  }

  private alpha2ToNationalityId(alpha2: string): string | null {
    if (alpha2 === "") {
      return null;
    }
    const match = this.nationalitiesStore.all().find(n => n.alpha2 === alpha2);
    return match?.id ?? null;
  }

  protected cancelRegistryEdit(): void {
    this.editingRegistryId.set(null);
    this.registryEditorOpen.set(false);
  }

  protected onRegistryDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelRegistryEdit();
    }
  }

  protected onRegistryDialogShown(): void {
    // Defer so the dialog content is mounted before we focus into it.
    queueMicrotask(() => this.lastNameInput()?.nativeElement.focus());
  }

  protected removeRegistryGuest(id: string): void {
    if (this.saving()) {
      return;
    }
    if (this.reservationId() === null) {
      this.localGuests.update(list => list.filter(g => g.id !== id));
      if (this.showsFeeColumn()) {
        this.localFeePayingIds.update(set => {
          if (!set.has(id)) {
            return set;
          }
          const next = new Set(set);
          next.delete(id);
          return next;
        });
      }
      return;
    }
    this.saving.set(true);
    this.guestsApi.remove(id).subscribe({
      next: () => {
        this.saving.set(false);
        this.mutated.emit();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  protected updateQuickAdd<K extends keyof QuickAddDraft>(
    key: K,
    value: QuickAddDraft[K]
  ): void {
    this.quickAdd.update(d => ({ ...d, [key]: value }));
  }

  protected submitQuickAdd(): void {
    if (!this.quickAddValid() || this.saving()) {
      return;
    }
    const cz = this.nationalitiesStore
      .all()
      .find(n => n.alpha2 === HOST_COUNTRY_ALPHA2);
    if (!cz) {
      return;
    }

    const d = this.quickAdd();
    const birth = d.birth;
    if (!birth) {
      return;
    }
    const resolved = this.resolveQuickAddAddress();

    if (this.reservationId() === null) {
      const newId = crypto.randomUUID();
      this.localGuests.update(list => [
        ...list,
        {
          id: newId,
          firstName: d.firstName.trim(),
          lastName: d.lastName.trim(),
          nationalityId: cz.id,
          birth: formatBirthDate(birth),
          documentType: DocumentType.IdCard,
          documentNumber: d.documentNumber.trim() || null,
          visaNumber: null,
          address: {
            street: resolved.street,
            houseNumber: resolved.houseNumber,
            zipCode: resolved.zipCode,
            city: resolved.city,
            countryCode: resolved.countryCode,
          },
          note: null,
        },
      ]);
      if (this.showsFeeColumn()) {
        const age = ageOnDate(birth, this.today);
        if (age === null || age >= FEE_ADULT_AGE_YEARS) {
          this.localFeePayingIds.update(set => {
            const next = new Set(set);
            next.add(newId);
            return next;
          });
        }
      }
      this.quickAdd.set({ ...EMPTY_QUICK_ADD });
      this.quickAddAddress.set(null);
      this.quickAddAddressInput.set("");
      return;
    }

    const reservationId = this.reservationId();
    if (!reservationId) {
      return;
    }
    const countryId = this.alpha2ToNationalityId(resolved.countryCode) ?? cz.id;
    const reservationFrom = this.reservationFrom();
    const reservationTo = this.reservationTo();
    const request: GuestRequest = {
      reservationId,
      billId: null,
      paysRecreationFee: null,
      firstName: d.firstName.trim(),
      lastName: d.lastName.trim(),
      nationalityId: cz.id,
      dateOfBirth: dateToIso(d.birth),
      documentType: DocumentType.IdCard as unknown as GuestDocumentType,
      documentNumber: d.documentNumber.trim(),
      address: {
        countryId,
        street: resolved.street,
        houseNumber: resolved.houseNumber,
        zipCode: resolved.zipCode,
        city: resolved.city,
      },
      reasonOfStay: "",
      stayDateRange: {
        from: reservationFrom ? dateToIso(reservationFrom) : "",
        to: reservationTo ? dateToIso(reservationTo) : "",
      },
      visaNumber: null,
      note: null,
      scartation: null,
      checkInAt: null,
      checkOutAt: null,
      signaturePngBase64: null,
    };

    this.saving.set(true);
    this.guestsApi.create(request).subscribe({
      next: () => {
        this.saving.set(false);
        this.quickAdd.set({ ...EMPTY_QUICK_ADD });
        this.quickAddAddress.set(null);
        this.quickAddAddressInput.set("");
        this.mutated.emit();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  private resolveQuickAddAddress(): GuestAddress {
    const picked = this.quickAddAddress();
    if (picked) {
      return {
        street: picked.street,
        houseNumber: picked.houseNumber,
        zipCode: picked.zipCode,
        city: picked.city,
        countryCode: picked.countryCode || HOST_COUNTRY_ALPHA2,
      };
    }
    const previous = this.lastGuestWithAddress();
    if (previous) {
      return { ...previous.address };
    }
    return {
      street: "",
      houseNumber: "",
      zipCode: "",
      city: "",
      countryCode: HOST_COUNTRY_ALPHA2,
    };
  }

  // Inline quick-add row is hard-coded for Czech guests, so always queries
  // the host-country (foreign=false) branch of the whisperer.
  protected onQuickAddAddressQuery(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < ADDRESS_WHISPERER_MIN_CHARS) {
      this.addressSuggestions.set([]);
      return;
    }
    this.addressesApi.whisperer(query, false).subscribe({
      next: results => this.addressSuggestions.set([...results]),
      error: () => this.addressSuggestions.set([]),
    });
  }

  protected onQuickAddAddressTyping(value: string | AddressSuggestion): void {
    if (typeof value === "string") {
      this.quickAddAddressInput.set(value);
      // Free-text edit invalidates a prior pick if the text differs.
      const picked = this.quickAddAddress();
      if (picked && this.formatAddressSummary(picked) !== value) {
        this.quickAddAddress.set(null);
      }
    } else {
      this.quickAddAddress.set(value);
      this.quickAddAddressInput.set(this.formatAddressSummary(value));
    }
  }

  protected onQuickAddAddressPick(event: AutoCompleteSelectEvent): void {
    const s = event.value as AddressSuggestion;
    this.quickAddAddress.set(s);
    this.quickAddAddressInput.set(this.formatAddressSummary(s));
  }

  private formatAddressSummary(s: AddressSuggestion): string {
    const street = [s.street, s.houseNumber].filter(Boolean).join(" ").trim();
    const cityLine = [s.zipCode, s.city].filter(Boolean).join(" ").trim();
    return [street, cityLine].filter(Boolean).join(", ");
  }

  protected onQuickAddAddressClear(): void {
    this.quickAddAddress.set(null);
    this.quickAddAddressInput.set("");
  }

  protected onTableScanKeyDown(event: KeyboardEvent): void {
    if (event.key === "Tab") {
      event.preventDefault();
      const input = event.target as HTMLInputElement;
      const pos = input.selectionStart ?? input.value.length;
      input.value = input.value.slice(0, pos) + "\t" + input.value.slice(pos);
      input.selectionStart = input.selectionEnd = pos + 1;
      input.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      this.tryParseTableScan();
    }
  }

  protected onTableScanInputChange(value: string): void {
    this.tableScanInput.set(value);
    this.tableScanError.set(null);
  }

  protected onTableScanBlur(): void {
    if (this.tableScanInput().includes("\t")) {
      this.tryParseTableScan();
    }
  }

  private tryParseTableScan(): void {
    const value = this.tableScanInput();
    if (!value.includes("\t")) {
      return;
    }
    const parts = value.split("\t");
    if (parts.length < 5) {
      this.tableScanError.set(
        "Skener vrátil neplatný formát (očekáváno 5 polí oddělených tabulátorem)."
      );
      this.tableScanInput.set("");
      return;
    }

    const [lastName, firstName, alpha3Raw, documentNumber, birthRaw] =
      parts as [string, string, string, string, string];
    const alpha3 = alpha3Raw.trim().toUpperCase();
    const nationality = this.nationalitiesStore
      .all()
      .find(n => n.alpha3 === alpha3);
    if (!nationality) {
      this.tableScanError.set(
        `Občanství „${alpha3}" nebylo v číselníku nalezeno.`
      );
      this.tableScanInput.set("");
      return;
    }
    const birth = parseScannerDate(birthRaw.trim());
    if (!birth) {
      this.tableScanError.set(
        `Datum narození „${birthRaw}" se nepodařilo rozpoznat.`
      );
      this.tableScanInput.set("");
      return;
    }

    if (nationality.alpha2 === HOST_COUNTRY_ALPHA2) {
      this.quickAdd.set({
        lastName: toTitleCase(lastName.trim()),
        firstName: toTitleCase(firstName.trim()),
        documentNumber: documentNumber.trim(),
        birth,
      });
    } else {
      // Document type is left blank; the receptionist picks it in the dialog.
      this.editingRegistryId.set(null);
      const draft = this.draftForNewGuest();
      this.registryDraft.set({
        ...draft,
        firstName: toTitleCase(firstName.trim()),
        lastName: toTitleCase(lastName.trim()),
        nationalityId: nationality.id,
        birth,
        documentNumber: documentNumber.trim(),
        address: {
          ...draft.address,
          countryCode:
            draft.address.countryCode === ""
              ? nationality.alpha2
              : draft.address.countryCode,
        },
      });
      this.scanInput.set("");
      this.scanError.set(null);
      this.registryEditorOpen.set(true);
    }

    this.tableScanInput.set("");
    this.tableScanError.set(null);
  }

  protected duplicateGuest(g: RegistryGuest): void {
    this.editingRegistryId.set(null);
    this.registryDraft.set({
      firstName: "",
      lastName: g.lastName,
      nationalityId: g.nationalityId,
      birth: null,
      documentType: null,
      documentNumber: "",
      biometric: false,
      visaNumber: "",
      address: { ...g.address },
      note: "",
    });
    this.scanInput.set("");
    this.scanError.set(null);
    this.registryEditorOpen.set(true);
  }

  protected onAddressQuery(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < ADDRESS_WHISPERER_MIN_CHARS) {
      this.addressSuggestions.set([]);
      return;
    }
    // CZ routes to the Czech registry, anything else to the foreign provider.
    const country = this.registryDraft().address.countryCode;
    if (country === "") {
      this.addressSuggestions.set([]);
      return;
    }
    const foreign = country !== HOST_COUNTRY_ALPHA2;
    this.addressesApi.whisperer(query, foreign).subscribe({
      next: results => this.addressSuggestions.set([...results]),
      error: () => this.addressSuggestions.set([]),
    });
  }

  protected onAddressPick(event: AutoCompleteSelectEvent): void {
    const s = event.value as AddressSuggestion;
    this.registryDraft.update(d => ({
      ...d,
      address: {
        street: s.street,
        houseNumber: s.houseNumber,
        zipCode: s.zipCode,
        city: s.city,
        countryCode: s.countryCode,
      },
    }));
  }

  protected formatAddressSuggestion(s: AddressSuggestion): string {
    const street = [s.street, s.houseNumber].filter(Boolean).join(" ").trim();
    const cityLine = [s.zipCode, s.city].filter(Boolean).join(" ").trim();
    return [street, cityLine, s.countryCode].filter(Boolean).join(", ");
  }

  private serializeVisa(d: RegistryDraft): string | null {
    if (d.documentType !== DocumentType.Passport) {
      return null;
    }
    const n = this.nationalitiesStore.byId().get(d.nationalityId);
    if (n?.biometricsRequired === true && d.biometric) {
      return BIOMETRIC_VISA_SENTINEL;
    }
    return nullableTrim(d.visaNumber);
  }

  // PrimeNG sets inline min-width on the overlay wrapper but leaves
  // max-width open, so wide options stretch the panel past the trigger.
  // Mirror min-width into max-width to pin the panel to the trigger.
  protected onOverlayShow(): void {
    queueMicrotask(() => {
      const wrappers = document.querySelectorAll<HTMLElement>(
        ".p-component.p-overlay"
      );
      for (const w of Array.from(wrappers)) {
        if (w.dataset["kempWidthPinned"] === "true") {
          continue;
        }
        const minWidth = w.style.minWidth;
        if (minWidth) {
          w.style.maxWidth = minWidth;
          w.dataset["kempWidthPinned"] = "true";
        }
      }
    });
  }
}

function emptyDraft(): RegistryDraft {
  return {
    ...EMPTY_REGISTRY_DRAFT,
    address: { ...EMPTY_REGISTRY_DRAFT.address },
  };
}

function hasAddressContent(address: GuestAddress): boolean {
  return Boolean(
    address.street || address.houseNumber || address.zipCode || address.city
  );
}

function nullableTrim(value: string): string | null {
  const t = value.trim();
  return t.length > 0 ? t : null;
}

function detailGuestToRegistryGuest(
  guest: ReservationDetailGuest,
  nationalitiesById: ReadonlyMap<string, Nationality>
): RegistryGuest {
  const addressCountry = nationalitiesById.get(guest.address.countryId);
  return {
    id: guest.id,
    firstName: guest.firstName,
    lastName: guest.lastName,
    nationalityId: guest.nationalityId,
    birth: formatBirthDate(parseIsoDate(guest.dateOfBirth) ?? new Date()),
    documentType:
      guest.documentType === null
        ? null
        : (guest.documentType as unknown as DocumentType),
    documentNumber: guest.documentNumber,
    visaNumber: guest.visaNumber,
    address: {
      street: guest.address.street,
      houseNumber: guest.address.houseNumber,
      zipCode: guest.address.zipCode,
      city: guest.address.city,
      countryCode: addressCountry?.alpha2 ?? "",
    },
    note: guest.note,
  };
}

function parseIsoDate(s: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
  if (!m) {
    return null;
  }
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
}

function formatBirthDate(d: Date): string {
  return `${d.getDate()}. ${d.getMonth() + 1}. ${d.getFullYear()}`;
}

function parseBirthDate(s: string): Date | null {
  const m = /^(\d+)\.\s*(\d+)\.\s*(\d+)$/.exec(s);
  if (!m) {
    return null;
  }
  return new Date(Number(m[3]), Number(m[2]) - 1, Number(m[1]));
}

// Scanners encode dates as DD.MM.YYYY, DD. MM. YYYY, YYYY-MM-DD,
// DDMMYYYY, or YYYYMMDD; accept the common ones.
function parseScannerDate(raw: string): Date | null {
  const text = raw.trim();
  if (text === "") {
    return null;
  }

  const iso = /^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$/.exec(text);
  if (iso) {
    return safeDate(Number(iso[1]), Number(iso[2]), Number(iso[3]));
  }

  const cz = /^(\d{1,2})\.\s*(\d{1,2})\.\s*(\d{4})$/.exec(text);
  if (cz) {
    return safeDate(Number(cz[3]), Number(cz[2]), Number(cz[1]));
  }

  const ddmmyyyy = /^(\d{2})(\d{2})(\d{4})$/.exec(text);
  if (ddmmyyyy) {
    return safeDate(
      Number(ddmmyyyy[3]),
      Number(ddmmyyyy[2]),
      Number(ddmmyyyy[1])
    );
  }

  const yyyymmdd = /^(\d{4})(\d{2})(\d{2})$/.exec(text);
  if (yyyymmdd) {
    return safeDate(
      Number(yyyymmdd[1]),
      Number(yyyymmdd[2]),
      Number(yyyymmdd[3])
    );
  }

  return null;
}

// Scanner emits names fully uppercased ("HORÁK"); convert to display
// casing preserving hyphens and apostrophes ("O'BRIEN" -> "O'Brien").
function toTitleCase(s: string): string {
  return s
    .toLocaleLowerCase("cs")
    .replace(
      /(^|[\s\-'])(\p{L})/gu,
      (_, sep: string, ch: string) => sep + ch.toLocaleUpperCase("cs")
    );
}

function safeDate(year: number, month: number, day: number): Date | null {
  if (month < 1 || month > 12 || day < 1 || day > 31) {
    return null;
  }
  const d = new Date(year, month - 1, day);
  // Reject silently rolled-over dates (e.g. 31.2.2000 -> 2.3.2000).
  if (
    d.getFullYear() !== year ||
    d.getMonth() !== month - 1 ||
    d.getDate() !== day
  ) {
    return null;
  }
  return d;
}
