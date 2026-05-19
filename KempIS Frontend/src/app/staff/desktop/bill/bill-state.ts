import { computed, inject, Injectable, signal } from "@angular/core";

import type {
  MealDay,
  PreloadedGuest,
  RecapRow,
  Tent,
  Vehicle,
} from "./bill-data";
import { equal as deepEqual } from "../../../../utils/deepEqual";
import { KNOWN_SERVICE_IDS } from "../../../core/services/known-service-ids";
import { ServicesStore } from "../../../core/services/services.store";
import { SpotGroupsStore } from "../../../core/spots/spot-groups.store";
import { SpotsStore } from "../../../core/spots/spots.store";
import { VatRatesStore } from "../../../core/vat-rates/vat-rates.store";
import { PaymentType } from "../../api/bills.types";
import type { ReservationDetailInvoice } from "../../api/reservations.types";
import type { RegistryGuest } from "../reservations/reservation-form/reservation-form-stub-data";
import { ServiceGroup } from "../system-settings/shared/service-groups";

/** Shared signal/computed options that swap Angular's default reference
 *  equality for structural deep equality. Several reservation-seeding
 *  effects re-call `.set(...)` with a freshly-allocated but structurally
 *  identical value (e.g. `[...arr.map(...)]` rebuilt on every reservation
 *  refresh); without deep equality those `.set`s invalidate downstream
 *  computeds and effects in a loop. With this in place a no-op write is
 *  truly a no-op. */
const DEEP_EQUAL = { equal: deepEqual };

export type PayerForm = {
  name: string;
  surname: string;
  street: string;
  houseNumber: string;
  city: string;
  zipCode: string;
  /** Alpha-2; resolved to countryId via NationalitiesStore at submit. */
  countryCode: string;
};

export type LegalEntityForm = {
  name: string;
  cin: string;
  tin: string;
  street: string;
  houseNumber: string;
  city: string;
  zipCode: string;
  countryCode: string;
};

const EMPTY_LEGAL_ENTITY: LegalEntityForm = {
  name: "",
  cin: "",
  tin: "",
  street: "",
  houseNumber: "",
  city: "",
  zipCode: "",
  countryCode: "CZ",
};

export type AccessCard = {
  readonly id: string;
  readonly uid: string;
  readonly deposit: number;
  readonly validUntil: string; // YYYY-MM-DD
  readonly note: string;
};

export type ReservationSpotItem = {
  readonly itemId: string;
  readonly spotId: string;
  readonly billId: string | null;
  readonly hasGivenKey: boolean;
  readonly hasReturnedKeys: boolean;
};

export type ReservationCottage = {
  readonly itemId: string;
  readonly spotId: string;
  readonly name: string;
  readonly groupName: string;
  readonly capacity: number;
  readonly nightly: number;
  readonly serviceId: string;
  readonly billId: string | null;
  readonly hasGivenKey: boolean;
  readonly hasReturnedKeys: boolean;
};

const EMPTY_PAYER: PayerForm = {
  name: "",
  surname: "",
  street: "",
  houseNumber: "",
  city: "",
  zipCode: "",
  countryCode: "CZ",
};

@Injectable()
export class BillState {
  private readonly servicesStore = inject(ServicesStore);
  private readonly spotsStore = inject(SpotsStore);
  private readonly spotGroupsStore = inject(SpotGroupsStore);
  private readonly vatRatesStore = inject(VatRatesStore);

  readonly from = signal<Date | null>(null, DEEP_EQUAL);
  readonly to = signal<Date | null>(null, DEEP_EQUAL);

  readonly nights = computed<number>(() => {
    const f = this.from();
    const t = this.to();
    if (!f || !t) {
      return 0;
    }
    const ms = t.getTime() - f.getTime();
    return Math.max(0, Math.round(ms / 86_400_000));
  });

  readonly preloadedGuests = signal<readonly PreloadedGuest[]>([], DEEP_EQUAL);

  /** Sourced from the services catalogue — `KNOWN_SERVICE_IDS.recreationFee`
   *  is the canonical entry; its `basePrice` is the per-person-night rate
   *  the recap row uses. Returns 0 until the services store has loaded so
   *  the bill total reflects "not yet available" rather than a hardcoded
   *  guess. */
  readonly recreationFeeRate = computed<number>(
    () =>
      this.servicesStore.byId(KNOWN_SERVICE_IDS.recreationFee)?.basePrice ?? 0
  );

  readonly feePayingPreloadedCount = computed(
    () => this.preloadedGuests().filter(g => g.checked && g.paysFee).length
  );

  readonly feePayingRegistryCount = signal<number>(0);

  readonly registryGuests = signal<readonly RegistryGuest[]>([], DEEP_EQUAL);
  readonly registryFeePayingIds = signal<ReadonlySet<string>>(
    new Set(),
    DEEP_EQUAL
  );

  readonly registryGuestSignatures = signal<ReadonlyMap<string, string>>(
    new Map(),
    DEEP_EQUAL
  );

  bufferRegistrySignature(id: string, pngBase64: string): void {
    this.registryGuestSignatures.update(prev => {
      const next = new Map(prev);
      if (pngBase64 === "") {
        next.delete(id);
      } else {
        next.set(id, pngBase64);
      }
      return next;
    });
  }

  readonly feePayingCount = computed(
    () => this.feePayingPreloadedCount() + this.feePayingRegistryCount()
  );

  readonly vehicles = signal<readonly Vehicle[]>([], DEEP_EQUAL);
  readonly caravans = signal<readonly Vehicle[]>([], DEEP_EQUAL);
  readonly unassignedVehicles = signal<readonly Vehicle[]>([], DEEP_EQUAL);
  readonly tents = signal<readonly Tent[]>([], DEEP_EQUAL);

  /** Latches true once the reservation seeding effect has run; the
   *  bucket-count guard alone can't distinguish first-load from
   *  everything-deleted because httpResource keeps serving the cached
   *  pre-delete reservation. */
  readonly vehiclesSeeded = signal<boolean>(false);

  /** Latches true once the duplicate seeding effect has run; without it
   *  the effect would re-seed on every services-store refresh. */
  readonly duplicateSeeded = signal<boolean>(false);

  readonly tentQtys = signal<ReadonlyMap<string, number>>(
    new Map(),
    DEEP_EQUAL
  );

  readonly reservationSpotItems = signal<readonly ReservationSpotItem[]>(
    [],
    DEEP_EQUAL
  );

  readonly selectedSpotItemIds = signal<ReadonlySet<string>>(
    new Set(),
    DEEP_EQUAL
  );

  // `{ equal: deepEqual }` is set so downstream subscribers don't re-run
  // when the fresh array assembled below is structurally identical to the
  // previous output.
  readonly reservationCottages = computed<readonly ReservationCottage[]>(() => {
    const spots = this.spotsStore.spots;
    if (!spots.hasValue()) {
      return [];
    }
    const spotById = new Map(spots.value().map(s => [s.id, s]));
    const groupsById = this.spotGroupsStore.byId();
    const rows: ReservationCottage[] = [];
    for (const item of this.reservationSpotItems()) {
      const spot = spotById.get(item.spotId);
      if (!spot) {
        continue;
      }
      const group = groupsById.get(spot.spotGroupId);
      if (!group) {
        continue;
      }
      const service = this.servicesStore.byId(group.serviceId);
      rows.push({
        itemId: item.itemId,
        spotId: item.spotId,
        name: spot.name,
        groupName: group.name,
        capacity: group.capacity,
        nightly: service?.basePrice ?? 0,
        serviceId: group.serviceId,
        billId: item.billId,
        hasGivenKey: item.hasGivenKey,
        hasReturnedKeys: item.hasReturnedKeys,
      });
    }
    return rows;
  }, DEEP_EQUAL);

  readonly accessCards = signal<readonly AccessCard[]>([], DEEP_EQUAL);

  readonly meals = signal<readonly MealDay[]>([], DEEP_EQUAL);

  readonly reservationInvoices = signal<readonly ReservationDetailInvoice[]>(
    [],
    DEEP_EQUAL
  );

  readonly linkedInvoiceIds = signal<ReadonlySet<string>>(
    new Set(),
    DEEP_EQUAL
  );

  readonly payer = signal<PayerForm>({ ...EMPTY_PAYER }, DEEP_EQUAL);

  readonly payerSourceId = signal<string | null>(null);

  readonly legalEntity = signal<LegalEntityForm | null>(null, DEEP_EQUAL);

  readonly legalEntityEnabled = computed(() => this.legalEntity() !== null);

  setLegalEntityEnabled(enabled: boolean): void {
    if (enabled) {
      if (this.legalEntity() === null) {
        this.legalEntity.set({ ...EMPTY_LEGAL_ENTITY });
      }
    } else {
      this.legalEntity.set(null);
    }
  }

  readonly paymentType = signal<PaymentType>(PaymentType.Card);
  readonly languageId = signal<string | null>(null);

  readonly printBill = signal<boolean>(false);

  readonly printBillCopies = signal<number>(1);

  readonly printTentStickers = signal<boolean>(true);

  readonly printTentStickerCopies = signal<number>(0);

  readonly serviceQtys = signal<ReadonlyMap<string, number>>(
    new Map(),
    DEEP_EQUAL
  );

  readonly serviceCatalogue = signal<
    ReadonlyMap<
      string,
      {
        name: string;
        basePrice: number;
        vatRate: number;
        serviceGroup: ServiceGroup;
      }
    >
  >(new Map(), DEEP_EQUAL);

  readonly recapOverrides = signal<ReadonlyMap<string, Partial<RecapRow>>>(
    new Map(),
    DEEP_EQUAL
  );

  readonly recapRemovedIds = signal<ReadonlySet<string>>(new Set(), DEEP_EQUAL);

  private vatForService(serviceId: string): number {
    const svc = this.servicesStore.byId(serviceId);
    if (!svc) {
      return 0;
    }
    return this.vatRatesStore.rateById().get(svc.vatRateId) ?? 0;
  }

  readonly recapRows = computed<readonly RecapRow[]>(() => {
    const rows: RecapRow[] = [];
    const nights = Math.max(1, this.nights());

    const selectedSpots = this.selectedSpotItemIds();
    type CottageBucket = {
      groupName: string;
      serviceId: string;
      nightly: number;
      vat: number;
      count: number;
    };
    const cottageBuckets = new Map<string, CottageBucket>();
    for (const c of this.reservationCottages()) {
      if (!selectedSpots.has(c.itemId)) {
        continue;
      }
      const key = `${c.serviceId}|${c.nightly}|${c.groupName}`;
      const existing = cottageBuckets.get(key);
      if (existing) {
        existing.count += 1;
        continue;
      }
      cottageBuckets.set(key, {
        groupName: c.groupName,
        serviceId: c.serviceId,
        nightly: c.nightly,
        vat: this.vatForService(c.serviceId),
        count: 1,
      });
    }
    for (const bucket of cottageBuckets.values()) {
      rows.push({
        id: `spot-group-${bucket.serviceId}`,
        service: bucket.groupName,
        days: nights,
        price: bucket.nightly,
        qty: bucket.count,
        vat: bucket.vat,
      });
    }

    type VehicleBucket = {
      type: string;
      serviceId: string;
      ratePerNight: number;
      count: number;
    };
    const collapseVehicles = (
      list: readonly Vehicle[],
      prefix: "veh" | "car"
    ): void => {
      const buckets = new Map<string, VehicleBucket>();
      for (const v of list) {
        if (v.serviceId === null) {
          continue;
        }
        const key = `${v.serviceId}|${v.ratePerNight}|${v.type}`;
        const existing = buckets.get(key);
        if (existing) {
          existing.count += 1;
          continue;
        }
        buckets.set(key, {
          type: v.type,
          serviceId: v.serviceId,
          ratePerNight: v.ratePerNight,
          count: 1,
        });
      }
      for (const bucket of buckets.values()) {
        rows.push({
          id: `${prefix}-group-${bucket.serviceId}`,
          service: bucket.type,
          days: nights,
          price: bucket.ratePerNight,
          qty: bucket.count,
          vat: this.vatForService(bucket.serviceId),
        });
      }
    };
    collapseVehicles(this.vehicles(), "veh");
    collapseVehicles(this.caravans(), "car");

    for (const t of this.tents().filter(t => t.qty > 0)) {
      rows.push({
        id: `tent-${t.id}`,
        service: t.label,
        days: t.nights,
        price: t.ratePerNight,
        qty: t.qty,
        vat: this.vatForService(t.id),
      });
    }

    const catalogue = this.serviceCatalogue();
    for (const [serviceId, qty] of this.serviceQtys()) {
      if (qty <= 0) {
        continue;
      }
      const svc = catalogue.get(serviceId);
      if (!svc) {
        continue;
      }
      rows.push({
        id: `svc-${serviceId}`,
        service: svc.name,
        days: svc.serviceGroup === ServiceGroup.Meals ? 1 : nights,
        price: svc.basePrice,
        qty,
        vat: svc.vatRate,
      });
    }

    const mealTotals = aggregateMealCounts(this.meals());
    const mealRows: readonly {
      readonly serviceId: string;
      readonly qty: number;
    }[] = [
      { serviceId: KNOWN_SERVICE_IDS.breakfast, qty: mealTotals.b },
      { serviceId: KNOWN_SERVICE_IDS.lunch, qty: mealTotals.l },
      { serviceId: KNOWN_SERVICE_IDS.lunchPackage, qty: mealTotals.lp },
      { serviceId: KNOWN_SERVICE_IDS.dinner, qty: mealTotals.d },
    ];
    for (const m of mealRows) {
      if (m.qty <= 0) {
        continue;
      }
      const svc = this.servicesStore.byId(m.serviceId);
      if (!svc) {
        continue;
      }
      rows.push({
        id: `meal-${m.serviceId}`,
        service: svc.name,
        days: 1,
        price: svc.basePrice,
        qty: m.qty,
        vat: this.vatForService(m.serviceId),
      });
    }

    const feeCount = this.feePayingCount();
    if (feeCount > 0) {
      rows.push({
        id: "rec-fee",
        service: "Rekreační poplatek",
        days: nights,
        price: this.recreationFeeRate(),
        qty: feeCount,
        vat: this.vatForService(KNOWN_SERVICE_IDS.recreationFee),
      });
    }

    return rows;
  }, DEEP_EQUAL);

  readonly finalRecapRows = computed<readonly RecapRow[]>(() => {
    const overrides = this.recapOverrides();
    const removed = this.recapRemovedIds();
    return this.recapRows()
      .filter(r => !removed.has(r.id))
      .map(r => {
        const ov = overrides.get(r.id);
        return ov ? { ...r, ...ov } : r;
      });
  }, DEEP_EQUAL);

  readonly grandTotal = computed<number>(() =>
    this.finalRecapRows().reduce(
      (sum, r) => sum + r.qty * Math.max(1, r.days) * r.price,
      0
    )
  );
}

function aggregateMealCounts(days: readonly MealDay[]): {
  b: number;
  l: number;
  lp: number;
  d: number;
} {
  return days.reduce(
    (acc, day) => {
      acc.b += day.b;
      acc.l += day.l;
      acc.lp += day.lp;
      acc.d += day.d;
      return acc;
    },
    { b: 0, l: 0, lp: 0, d: 0 }
  );
}
