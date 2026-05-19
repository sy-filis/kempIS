import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  applyWhen,
  form,
  FormField,
  provideSignalFormsConfig,
  required,
  submit,
  validate,
} from "@angular/forms/signals";

import { ButtonModule } from "primeng/button";
import { CheckboxModule } from "primeng/checkbox";
import { DatePickerModule } from "primeng/datepicker";
import { FloatLabelModule } from "primeng/floatlabel";
import { InputTextModule } from "primeng/inputtext";
import { RadioButtonModule } from "primeng/radiobutton";
import { SelectModule } from "primeng/select";
import { TagModule } from "primeng/tag";
import { firstValueFrom } from "rxjs";

import { toWireDto } from "./checkin-payload";
import {
  DocumentType,
  documentTypesForNationality,
  HOST_COUNTRY_ALPHA2,
} from "./document-types";
import { ApiClient } from "../../../core/api/api-client";
import { calculateAgeYears } from "../../../shared/calculate-age";
import { dateToIso } from "../../../shared/date-iso";
import { SignaturePadComponent } from "../../../shared/signature-pad/signature-pad.component";
import type { Nationality } from "../api/public-reservations.types";

type CheckInModel = {
  firstName: string;
  lastName: string;
  birthDate: string; // ISO YYYY-MM-DD
  street: string;
  houseNumber: string;
  zipCode: string;
  city: string;
  countryId: string;
  nationalityId: string;
  documentType: DocumentType | null;
  documentNumber: string;
  biometric: boolean;
  visaNumber: string;
  plates: string[];
  consent: boolean;
  signaturePngBase64: string;
};

const PLATE_PATTERN = /^[A-Z0-9-]{4,12}$/;
const VISA_PATTERN = /^[A-Z]{1,3}[0-9]+$/;

@Component({
  selector: "kemp-is-online-checkin",
  imports: [
    FormsModule,
    FormField,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    FloatLabelModule,
    InputTextModule,
    RadioButtonModule,
    SelectModule,
    SignaturePadComponent,
    TagModule,
  ],
  templateUrl: "./online-checkin.page.html",
  styleUrl: "./online-checkin.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    provideSignalFormsConfig({
      classes: {
        "kemp-touched": field => field.state().touched(),
      },
    }),
  ],
})
export class OnlineCheckinPage {
  private readonly apiClient = inject(ApiClient);

  readonly id = input.required<string>();
  readonly secret = input.required<string>();

  protected readonly nationalities = httpResource<readonly Nationality[]>(() =>
    this.apiClient.url("/nationalities")
  );

  private readonly nationalityList = computed<readonly Nationality[]>(() =>
    this.nationalities.hasValue() ? this.nationalities.value() : []
  );

  protected readonly nationalityOptions = computed<Nationality[]>(() =>
    [...this.nationalityList()].sort((a, b) => a.name.localeCompare(b.name))
  );

  protected readonly model = signal<CheckInModel>({
    firstName: "",
    lastName: "",
    birthDate: "",
    street: "",
    houseNumber: "",
    zipCode: "",
    city: "",
    countryId: "",
    nationalityId: "",
    documentType: null,
    documentNumber: "",
    biometric: false,
    visaNumber: "",
    plates: [""],
    consent: false,
    signaturePngBase64: "",
  });

  // PrimeNG datepicker binds a Date; mirrored onto model.birthDate (ISO string) via effect.
  protected readonly birthDateValue = signal<Date | null>(null);

  private readonly selectedNationality = computed(() =>
    this.nationalityList().find(n => n.id === this.model().nationalityId)
  );

  protected readonly isHostCountryCitizen = computed(
    () => this.selectedNationality()?.alpha2 === HOST_COUNTRY_ALPHA2
  );

  protected readonly age = computed<number | null>(() =>
    calculateAgeYears(this.model().birthDate, this.today)
  );

  protected readonly documentOptional = computed(
    () => this.isHostCountryCitizen() && (this.age() ?? Infinity) < 15
  );

  protected readonly showDocumentPicker = computed(
    () => this.model().nationalityId !== ""
  );

  protected readonly isPassport = computed(
    () => this.model().documentType === DocumentType.Passport
  );

  protected readonly showBiometric = computed(
    () =>
      this.isPassport() &&
      this.selectedNationality()?.biometricsRequired === true
  );

  protected readonly allowedDocumentTypes = computed<readonly DocumentType[]>(
    () => documentTypesForNationality(this.selectedNationality())
  );

  protected readonly documentTypeOptions = computed(() =>
    this.allowedDocumentTypes().map(value => ({
      value,
      label: this.documentTypeLabel(value),
    }))
  );

  private documentTypeLabel(type: DocumentType): string {
    switch (type) {
      case DocumentType.Passport:
        return $localize`:@@checkin.document-type.passport:Cestovní pas`;
      case DocumentType.IdCard:
        return $localize`:@@checkin.document-type.id:Občanský průkaz`;
      case DocumentType.CzechResidencePermit:
        return $localize`:@@checkin.document-type.cz-residence-permit:Povolení k pobytu v ČR`;
      case DocumentType.ForeignEuResidencePermit:
        return $localize`:@@checkin.document-type.eu-residence-permit:Povolení k pobytu v EU`;
      case DocumentType.LostPassportConfirmation:
        return $localize`:@@checkin.document-type.lost-passport:Náhradní cestovní doklad`;
      case DocumentType.CzechDiplomatCard:
        return $localize`:@@checkin.document-type.cz-diplomat:Diplomatický průkaz ČR`;
      case DocumentType.ChildInParentPassport:
        return $localize`:@@checkin.document-type.child-in-parent-passport:Dítě zapsané v pasu rodiče`;
      default: {
        const _exhaustive: never = type;
        return _exhaustive;
      }
    }
  }

  protected readonly requiresSignature = computed(() => {
    if (!this.model().nationalityId) {
      return false;
    }
    return !this.isHostCountryCitizen();
  });

  // Visa is recorded for a non-biometric passport when the nationality requires a visa,
  // or grants visa-free travel only with a biometric passport (e.g. Albania, Bosnia).
  protected readonly requiresVisa = computed(() => {
    if (!this.isPassport() || this.model().biometric) {
      return false;
    }
    const nationality = this.selectedNationality();
    return (
      nationality?.visaRequired === true ||
      nationality?.biometricsRequired === true
    );
  });

  protected readonly documentNumberLabel = computed(() =>
    this.isPassport()
      ? $localize`:@@checkin.document.passport-number:Číslo pasu`
      : $localize`:@@checkin.document.id-number:Číslo dokladu`
  );

  protected readonly form = form(this.model, s => {
    required(s.firstName, {
      message: $localize`:@@checkin.first-name.required:Vyplňte jméno`,
    });
    required(s.lastName, {
      message: $localize`:@@checkin.surname.required:Vyplňte příjmení`,
    });
    required(s.birthDate, {
      message: $localize`:@@checkin.birth-date.required:Vyplňte datum narození`,
    });
    required(s.street, {
      message: $localize`:@@checkin.street.required:Vyplňte ulici`,
    });
    required(s.houseNumber, {
      message: $localize`:@@checkin.house-number.required:Vyplňte č.p.`,
    });
    required(s.zipCode, {
      message: $localize`:@@checkin.zip.required:Vyplňte PSČ`,
    });
    required(s.city, {
      message: $localize`:@@checkin.city.required:Vyplňte město`,
    });
    required(s.countryId, {
      message: $localize`:@@checkin.country.required:Vyberte zemi`,
    });
    required(s.nationalityId, {
      message: $localize`:@@checkin.nationality.required:Vyberte občanství`,
    });

    applyWhen(
      s,
      () => this.showDocumentPicker() && !this.documentOptional(),
      sub => {
        required(sub.documentType, {
          message: $localize`:@@checkin.document-type.required:Vyberte typ dokladu`,
        });
      }
    );

    applyWhen(
      s,
      () => this.model().documentType !== null && !this.documentOptional(),
      sub => {
        required(sub.documentNumber, {
          message: $localize`:@@checkin.document-number.required:Vyplňte číslo dokladu`,
        });
      }
    );

    applyWhen(
      s,
      () => this.requiresVisa(),
      sub => {
        required(sub.visaNumber, {
          message: $localize`:@@checkin.visa-number.required:Vyplňte číslo víza`,
        });
        validate(sub.visaNumber, ({ value }) =>
          VISA_PATTERN.test(value())
            ? undefined
            : {
                kind: "visa-format",
                message: $localize`:@@checkin.visa-number.format:Číslo víza ve formátu např. AB1234567`,
              }
        );
      }
    );

    validate(s.plates, ({ value }) => {
      const invalid = value().some(
        p => p.trim().length > 0 && !PLATE_PATTERN.test(p.trim())
      );
      return invalid
        ? {
            kind: "plate",
            message: $localize`:@@checkin.plate.invalid:Neplatné SPZ`,
          }
        : undefined;
    });

    validate(s.consent, ({ value }) =>
      value()
        ? undefined
        : {
            kind: "consent",
            message: $localize`:@@checkin.consent.required:Potvrďte souhlas`,
          }
    );

    applyWhen(
      s,
      () => this.requiresSignature(),
      sub => {
        required(sub.signaturePngBase64, {
          message: $localize`:@@checkin.signature.required:Podepište se prosím`,
        });
      }
    );
  });

  protected readonly formInvalid = computed(() => this.form().invalid());

  protected readonly nationalityListEmpty = computed(
    () => !this.nationalities.isLoading() && this.nationalityList().length === 0
  );

  protected readonly submitting = signal(false);

  protected readonly submitOutcome = signal<
    { kind: "idle" } | { kind: "success" } | { kind: "error"; message: string }
  >({ kind: "idle" });

  protected readonly submitLabel = $localize`:@@checkin.submit:Odeslat check-in`;
  protected readonly saveDraftLabel = $localize`:@@checkin.save-draft:Uložit a dokončit později`;
  protected readonly addPlateLabel = $localize`:@@checkin.plate.add:Přidat další SPZ`;
  protected readonly removePlateLabel = $localize`:@@checkin.plate.remove:Odebrat SPZ`;
  protected readonly visaTagLabel = $localize`:@@checkin.visa.tag:Pas s výzovou povinností`;
  protected readonly passportTypeLegend = $localize`:@@checkin.passport-type.legend:Typ pasu`;

  protected readonly today = new Date();

  constructor() {
    effect(() => {
      const d = this.birthDateValue();
      const iso = d ? dateToIso(d) : "";
      this.model.update(m =>
        m.birthDate === iso ? m : { ...m, birthDate: iso }
      );
    });

    effect(() => {
      if (!this.isPassport()) {
        this.model.update(m =>
          !m.biometric && m.visaNumber === ""
            ? m
            : { ...m, biometric: false, visaNumber: "" }
        );
      }
    });

    effect(() => {
      if (!this.requiresVisa() && this.model().visaNumber !== "") {
        this.model.update(m => ({ ...m, visaNumber: "" }));
      }
    });

    effect(() => {
      if (!this.requiresSignature() && this.model().signaturePngBase64) {
        this.model.update(m => ({ ...m, signaturePngBase64: "" }));
      }
    });

    // Effect (not ngModelChange) preserves [formField] touched/dirty tracking.
    effect(() => {
      const current = this.model().visaNumber;
      const upper = current.toUpperCase();
      if (current !== upper) {
        this.model.update(m => ({ ...m, visaNumber: upper }));
      }
    });

    effect(() => {
      if (!this.showBiometric() && this.model().biometric) {
        this.model.update(m => ({ ...m, biometric: false }));
      }
    });

    effect(() => {
      const allowed = this.allowedDocumentTypes();
      const current = this.model().documentType;
      if (current !== null && !allowed.includes(current)) {
        this.model.update(m => ({ ...m, documentType: null }));
      }
    });
  }

  protected setBiometric(value: boolean): void {
    this.model.update(m =>
      m.biometric === value ? m : { ...m, biometric: value }
    );
  }

  protected addPlate(): void {
    this.model.update(m => ({ ...m, plates: [...m.plates, ""] }));
  }

  protected removePlate(index: number): void {
    this.model.update(m => {
      const next = m.plates.filter((_, i) => i !== index);
      return { ...m, plates: next.length > 0 ? next : [""] };
    });
  }

  protected updatePlate(index: number, value: string | null): void {
    const next = (value ?? "").toUpperCase();
    this.model.update(m => ({
      ...m,
      plates: m.plates.map((p, i) => (i === index ? next : p)),
    }));
  }

  protected onSignatureChange(value: string): void {
    this.model.update(m =>
      m.signaturePngBase64 === value ? m : { ...m, signaturePngBase64: value }
    );
  }

  protected onSubmit(): void {
    submit(this.form, async () => {
      this.submitting.set(true);
      this.submitOutcome.set({ kind: "idle" });
      try {
        await firstValueFrom(
          this.apiClient.post(
            `/reservations/${this.id()}/guest/check-in`,
            toWireDto(this.model()),
            { params: { secret: this.secret() } }
          )
        );
        this.submitOutcome.set({ kind: "success" });
      } catch (err) {
        this.submitOutcome.set({
          kind: "error",
          message: this.extractErrorMessage(err),
        });
      } finally {
        this.submitting.set(false);
      }
    });
  }

  private extractErrorMessage(err: unknown): string {
    const body = (err as { error?: unknown } | null)?.error;
    if (typeof body === "object" && body !== null && "message" in body) {
      const inner = (body as { message?: unknown }).message;
      if (typeof inner === "string" && inner.length > 0) {
        return inner;
      }
    }
    if (err instanceof Error && err.message.length > 0) {
      return err.message;
    }
    return $localize`:@@checkin.error.generic:Odeslání se nezdařilo, zkuste to prosím znovu.`;
  }

  protected onSaveDraft(): void {
    // Draft endpoint not yet exposed by the backend.
  }
}
