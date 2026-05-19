import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { CheckboxModule } from "primeng/checkbox";
import { DatePickerModule } from "primeng/datepicker";
import { DialogModule } from "primeng/dialog";
import { InputNumberModule } from "primeng/inputnumber";
import { InputTextModule } from "primeng/inputtext";
import { SelectModule } from "primeng/select";

import { StepPeriodGuestsStub } from "../../reservations/reservation-form/steps/step-period-guests-stub";
import {
  COUNTRIES,
  type Country,
  DOC_TYPES,
  type DocType,
  type DocTypeOption,
  FEE_CATEGORIES,
  type FeeCategory,
  type FeeCategoryId,
  type PreloadedGuest,
} from "../bill-data";
import { BillState } from "../bill-state";

type PreloadedDraft = {
  firstName: string;
  surname: string;
  birth: Date | null;
  street: string;
  houseNumber: string;
  postalCode: string;
  city: string;
  country: string;
  citizenship: string;
  docType: DocType;
  docNumber: string;
  fee: FeeCategoryId;
};

const EMPTY_PRELOADED_DRAFT: PreloadedDraft = {
  firstName: "",
  surname: "",
  birth: null,
  street: "",
  houseNumber: "",
  postalCode: "",
  city: "",
  country: "CZ",
  citizenship: "CZ",
  docType: "op",
  docNumber: "",
  fee: "adult",
};

@Component({
  selector: "kemp-is-bill-step1-period",
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    StepPeriodGuestsStub,
  ],
  templateUrl: "./step1-period.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Step1Period {
  /** False when the bill inherits its period from a parent reservation. */
  public readonly periodEditable = input<boolean>(true);

  private readonly billState = inject(BillState);

  protected readonly from = this.billState.from;
  protected readonly to = this.billState.to;
  protected readonly nights = this.billState.nights;

  protected readonly recreationFeeRate = this.billState.recreationFeeRate;

  protected onNightsChange(n: number | null): void {
    const f = this.from();
    if (!f) {
      return;
    }
    const next = Math.max(0, n ?? 0);
    this.to.set(new Date(f.getFullYear(), f.getMonth(), f.getDate() + next));
  }

  protected readonly preloadedGuests = this.billState.preloadedGuests;

  protected readonly feeCategoryOptions: FeeCategory[] = [...FEE_CATEGORIES];
  protected readonly countryOptions: Country[] = [...COUNTRIES];
  protected readonly docTypeOptions: DocTypeOption[] = [...DOC_TYPES];

  protected readonly guestEditorOpen = signal<boolean>(false);
  protected readonly editingPreloadedId = signal<string | null>(null);
  protected readonly preloadedDraft = signal<PreloadedDraft>({
    ...EMPTY_PRELOADED_DRAFT,
  });
  protected readonly preloadedDraftValid = computed(
    () =>
      this.preloadedDraft().firstName.trim().length > 0 &&
      this.preloadedDraft().surname.trim().length > 0
  );

  protected readonly feePayingRegistryCount =
    this.billState.feePayingRegistryCount;

  protected readonly registryGuests = this.billState.registryGuests;
  protected readonly registryFeePayingIds = this.billState.registryFeePayingIds;

  protected readonly linkedPreloadedCount = computed(
    () => this.preloadedGuests().filter(g => g.checked).length
  );

  protected readonly feePayingPreloadedCount =
    this.billState.feePayingPreloadedCount;

  protected readonly feePayingCount = this.billState.feePayingCount;

  protected readonly feeTotal = computed(
    () => this.feePayingCount() * this.recreationFeeRate() * this.nights()
  );

  protected toggleGuest(id: string): void {
    this.preloadedGuests.update(list =>
      list.map(g => (g.id === id ? { ...g, checked: !g.checked } : g))
    );
  }

  protected togglePaysFee(id: string): void {
    this.preloadedGuests.update(list =>
      list.map(g => (g.id === id ? { ...g, paysFee: !g.paysFee } : g))
    );
  }

  protected openPreloadedEditor(event: MouseEvent, g: PreloadedGuest): void {
    event.stopPropagation();
    event.preventDefault();
    this.editingPreloadedId.set(g.id);
    this.preloadedDraft.set({
      firstName: g.firstName,
      surname: g.surname,
      birth: parseBirthDate(g.birth),
      street: g.street,
      houseNumber: g.houseNumber,
      postalCode: g.postalCode,
      city: g.city,
      country: g.country,
      citizenship: g.citizenship,
      docType: g.docType,
      docNumber: g.docNumber,
      fee: g.fee,
    });
    this.guestEditorOpen.set(true);
  }

  protected updatePreloadedDraft<K extends keyof PreloadedDraft>(
    key: K,
    value: PreloadedDraft[K]
  ): void {
    this.preloadedDraft.update(d => ({ ...d, [key]: value }));
  }

  protected savePreloadedEdit(): void {
    const id = this.editingPreloadedId();
    if (id === null || !this.preloadedDraftValid()) {
      return;
    }
    const d = this.preloadedDraft();
    this.preloadedGuests.update(list =>
      list.map(g =>
        g.id === id
          ? {
              ...g,
              firstName: d.firstName.trim(),
              surname: d.surname.trim(),
              birth: d.birth ? formatBirthDate(d.birth) : g.birth,
              street: d.street.trim(),
              houseNumber: d.houseNumber.trim(),
              postalCode: d.postalCode.trim(),
              city: d.city.trim(),
              country: d.country,
              citizenship: d.citizenship,
              docType: d.docType,
              docNumber: d.docNumber.trim(),
              fee: d.fee,
            }
          : g
      )
    );
    this.cancelPreloadedEdit();
  }

  protected cancelPreloadedEdit(): void {
    this.editingPreloadedId.set(null);
    this.guestEditorOpen.set(false);
  }

  protected onGuestDialogVisibleChange(visible: boolean): void {
    if (!visible) {
      this.cancelPreloadedEdit();
    }
  }

  protected ageOf(g: PreloadedGuest): number {
    return ageFromBirth(g.birth);
  }

  protected ageFromBirthString(birth: string): number {
    return ageFromBirth(birth);
  }

  protected feeFor(id: FeeCategoryId): { label: string; rate: number } {
    const f = FEE_CATEGORIES.find(c => c.id === id);
    return { label: f?.label ?? "", rate: f?.rate ?? 0 };
  }

  protected formatNumber(n: number): string {
    return n.toLocaleString("cs-CZ");
  }
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

function ageFromBirth(birth: string, today: Date = new Date()): number {
  const d = parseBirthDate(birth);
  if (!d) {
    return 0;
  }
  let age = today.getFullYear() - d.getFullYear();
  const monthDiff = today.getMonth() - d.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < d.getDate())) {
    age -= 1;
  }
  return Math.max(0, age);
}
