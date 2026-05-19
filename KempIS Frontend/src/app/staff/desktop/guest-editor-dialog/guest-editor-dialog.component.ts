import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  model,
  output,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";

import { MessageService } from "primeng/api";
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
import { MessageModule } from "primeng/message";
import { SelectModule } from "primeng/select";
import { TextareaModule } from "primeng/textarea";

import {
  AddressesApi,
  type AddressSuggestion,
} from "../../../core/addresses/addresses.api";
import { ApiClient } from "../../../core/api/api-client";
import { NationalitiesStore } from "../../../core/nationalities/nationalities.store";
import { dateToIso, isoToDate } from "../../../shared/date-iso";
import { type GuestRequest, GuestsApi } from "../../api/guests.api";
import type { GuestDetail } from "../../api/guests.types";
import { GuestDocumentType } from "../../api/reservations.types";

export type GuestEditorFullDetails = {
  readonly nationalityId: string;
  readonly dateOfBirth: string;
  readonly documentType: GuestDocumentType | null;
  readonly documentNumber: string | null;
  readonly address: {
    readonly countryId: string;
    readonly city: string;
    readonly zipCode: string;
    readonly street: string;
    readonly houseNumber: string;
  };
  readonly reasonOfStay: string;
  readonly stayFrom: string;
  readonly stayTo: string;
  readonly visaNumber: string | null;
  readonly note: string | null;
  readonly scartation: string | null;
  readonly checkInAt: string | null;
  readonly checkOutAt: string | null;
};

export type GuestEditorInput = {
  readonly id: string;
  readonly reservationId: string | null;
  readonly billId: string | null;
  readonly firstName: string;
  readonly lastName: string;
  readonly paysRecreationFee: boolean | null;
  readonly full: GuestEditorFullDetails | null;
};

type GuestDraft = {
  firstName: string;
  lastName: string;
  birth: Date | null;
  nationalityId: string;
  documentType: GuestDocumentType;
  documentNumber: string;
  reasonOfStay: string;
  street: string;
  houseNumber: string;
  city: string;
  zipCode: string;
  countryId: string;
  visaNumber: string;
  note: string;
  paysRecreationFee: boolean;
};

const EMPTY_DRAFT: GuestDraft = {
  firstName: "",
  lastName: "",
  birth: null,
  nationalityId: "",
  documentType: GuestDocumentType.IdCard,
  documentNumber: "",
  reasonOfStay: "Rekreace",
  street: "",
  houseNumber: "",
  city: "",
  zipCode: "",
  countryId: "",
  visaNumber: "",
  note: "",
  paysRecreationFee: false,
};

@Component({
  selector: "kemp-is-guest-editor-dialog",
  imports: [
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: "./guest-editor-dialog.component.html",
  styleUrl: "./guest-editor-dialog.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuestEditorDialogComponent {
  private readonly guestsApi = inject(GuestsApi);
  private readonly nationalitiesStore = inject(NationalitiesStore);
  private readonly addressesApi = inject(AddressesApi);
  private readonly apiClient = inject(ApiClient);
  private readonly messages = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly addressSuggestions = signal<AddressSuggestion[]>([]);
  protected readonly minAddressChars = 8;

  readonly visible = model<boolean>(false);

  readonly guest = input<GuestEditorInput | null>(null);

  readonly saved = output<string>();

  protected readonly saving = signal<boolean>(false);
  protected readonly draft = signal<GuestDraft>({ ...EMPTY_DRAFT });

  protected readonly detail = httpResource<GuestDetail>(() => {
    const g = this.guest();
    return g
      ? this.apiClient.url(`/guests/${encodeURIComponent(g.id)}`)
      : undefined;
  });

  protected readonly detailLoading = computed(() => this.detail.isLoading());

  protected readonly detailError = computed(() => {
    const err = this.detail.error();
    if (err === undefined) {
      return null;
    }
    return err instanceof Error
      ? err.message
      : "Načtení detailu hosta selhalo.";
  });

  protected readonly nationalityOptions = computed(() =>
    this.nationalitiesStore.all().map(n => ({ id: n.id, name: n.name }))
  );

  protected readonly documentTypeOptions = [
    { id: GuestDocumentType.IdCard, name: "Občanský průkaz" },
    { id: GuestDocumentType.Passport, name: "Cestovní pas" },
    {
      id: GuestDocumentType.CzechResidencePermit,
      name: "Povolení k pobytu (CZ)",
    },
    {
      id: GuestDocumentType.LostPassportConfirmation,
      name: "Potvrzení o ztrátě pasu",
    },
    {
      id: GuestDocumentType.CzechDiplomatCard,
      name: "Diplomatická karta (CZ)",
    },
    {
      id: GuestDocumentType.ChildInParentPassport,
      name: "Dítě zapsané v pase rodiče",
    },
  ];

  constructor() {
    // Draft seed priority: GET /guests/{id} > caller `full` block > bare name+RP flag.
    effect(() => {
      const g = this.guest();
      if (!g) {
        this.draft.set({ ...EMPTY_DRAFT });
        return;
      }
      if (this.detail.hasValue()) {
        const f = this.detail.value();
        this.draft.set({
          firstName: f.firstName,
          lastName: f.lastName,
          birth: isoToDate(f.dateOfBirth),
          nationalityId: f.nationalityId,
          documentType:
            (f.documentType as GuestDocumentType | null) ??
            GuestDocumentType.IdCard,
          documentNumber: f.documentNumber ?? "",
          reasonOfStay: f.reasonOfStay,
          street: f.address.street,
          houseNumber: f.address.houseNumber,
          city: f.address.city,
          zipCode: f.address.zipCode,
          countryId: f.address.countryId,
          visaNumber: f.visaNumber ?? "",
          note: f.note ?? "",
          paysRecreationFee: f.paysRecreationFee ?? false,
        });
        return;
      }
      if (g.full) {
        const f = g.full;
        this.draft.set({
          firstName: g.firstName,
          lastName: g.lastName,
          birth: isoToDate(f.dateOfBirth),
          nationalityId: f.nationalityId,
          documentType: f.documentType ?? GuestDocumentType.IdCard,
          documentNumber: f.documentNumber ?? "",
          reasonOfStay: f.reasonOfStay,
          street: f.address.street,
          houseNumber: f.address.houseNumber,
          city: f.address.city,
          zipCode: f.address.zipCode,
          countryId: f.address.countryId,
          visaNumber: f.visaNumber ?? "",
          note: f.note ?? "",
          paysRecreationFee: g.paysRecreationFee ?? false,
        });
      } else {
        this.draft.set({
          ...EMPTY_DRAFT,
          firstName: g.firstName,
          lastName: g.lastName,
          paysRecreationFee: g.paysRecreationFee ?? false,
        });
      }
    });
  }

  protected updateDraft<K extends keyof GuestDraft>(
    key: K,
    value: GuestDraft[K]
  ): void {
    this.draft.update(d => ({ ...d, [key]: value }));
  }

  protected onVisibleChange(next: boolean): void {
    this.visible.set(next);
  }

  private isForeign(): boolean {
    const id = this.draft().countryId;
    if (id === "") {
      return false;
    }
    const alpha2 = this.nationalitiesStore.byId().get(id)?.alpha2 ?? "CZ";
    return alpha2 !== "CZ";
  }

  protected onStreetType(value: string | AddressSuggestion): void {
    if (typeof value === "string") {
      this.updateDraft("street", value);
    }
  }

  protected onAddressQuery(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < this.minAddressChars) {
      this.addressSuggestions.set([]);
      return;
    }
    this.addressesApi
      .whisperer(query, this.isForeign())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: results => this.addressSuggestions.set([...results]),
        error: () => this.addressSuggestions.set([]),
      });
  }

  protected onAddressPick(event: AutoCompleteSelectEvent): void {
    const s = event.value as AddressSuggestion;
    const idFromAlpha2 =
      this.nationalitiesStore.all().find(n => n.alpha2 === s.countryCode)?.id ??
      this.draft().countryId;
    this.draft.update(d => ({
      ...d,
      street: s.street,
      houseNumber: s.houseNumber,
      zipCode: s.zipCode,
      city: s.city,
      countryId: idFromAlpha2,
    }));
  }

  protected save(): void {
    const g = this.guest();
    if (!g || this.saving()) {
      return;
    }
    const draft = this.draft();
    if (
      draft.firstName.trim() === "" ||
      draft.lastName.trim() === "" ||
      draft.birth === null
    ) {
      this.messages.add({
        severity: "warn",
        summary: "Neúplný host",
        detail: "Vyplňte jméno, příjmení a datum narození.",
      });
      return;
    }
    const detail = this.detail.hasValue() ? this.detail.value() : null;
    const stayFrom = detail?.stayDateRange?.from ?? g.full?.stayFrom ?? "";
    const stayTo = detail?.stayDateRange?.to ?? g.full?.stayTo ?? "";
    const request: GuestRequest = {
      reservationId: g.reservationId,
      billId: g.billId,
      paysRecreationFee: draft.paysRecreationFee,
      firstName: draft.firstName.trim(),
      lastName: draft.lastName.trim(),
      nationalityId: draft.nationalityId,
      dateOfBirth: dateToIso(draft.birth),
      documentType: draft.documentType,
      documentNumber: draft.documentNumber.trim(),
      address: {
        countryId: draft.countryId,
        city: draft.city.trim(),
        zipCode: draft.zipCode.trim(),
        street: draft.street.trim(),
        houseNumber: draft.houseNumber.trim(),
      },
      reasonOfStay:
        draft.reasonOfStay.trim() === "" ? "Rekreace" : draft.reasonOfStay,
      stayDateRange: { from: stayFrom, to: stayTo },
      visaNumber: draft.visaNumber.trim() === "" ? null : draft.visaNumber,
      note: draft.note.trim() === "" ? null : draft.note,
      scartation: detail?.scartation ?? g.full?.scartation ?? null,
      checkInAt: detail?.checkInAt ?? g.full?.checkInAt ?? null,
      checkOutAt: detail?.checkOutAt ?? g.full?.checkOutAt ?? null,
      signaturePngBase64: null,
    };
    this.saving.set(true);
    const guestId = g.id;
    this.guestsApi
      .update(guestId, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.messages.add({
            severity: "success",
            summary: "Host uložen",
            detail: "Změny byly úspěšně uloženy.",
          });
          this.visible.set(false);
          this.saved.emit(guestId);
        },
        error: () => {
          this.saving.set(false);
          this.messages.add({
            severity: "error",
            summary: "Host",
            detail: "Změny se nepodařilo uložit.",
          });
        },
      });
  }
}
