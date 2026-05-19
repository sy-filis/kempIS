import type {
  MealAmountDto,
  MealResponse,
  ReplaceMealRequest,
} from "../../api/meals.types";
import type { Reservation } from "../../api/reservations.types";

export type WeekPickerEntry = {
  readonly year: number;
  readonly week: number;
  readonly label: string;
  readonly range: string;
  readonly current: boolean;
};

export type WeekDay = {
  readonly key: DayKey;
  readonly iso: string;
  readonly dow: string;
  readonly name: string;
  readonly d: number;
  readonly m: number;
  readonly label: string;
  readonly today?: boolean;
  readonly weekend?: boolean;
};

export type MealsMeta = {
  readonly weekLabel: string;
  readonly weekNo: number;
  readonly year: number;
  readonly weekFrom: string;
  readonly weekTo: string;
};

export type DayKey = "mon" | "tue" | "wed" | "thu" | "fri" | "sat" | "sun";

export type MealKey = "S" | "O" | "OB" | "V";

export type MealType = {
  readonly key: MealKey;
  readonly label: string;
  readonly short: string;
  readonly icon: string;
};

export type VariantKey = "N" | "V" | "B" | "L" | "BL" | "BV" | "LV" | "BLV";

export type DietVariant = {
  readonly key: VariantKey;
  readonly label: string;
  readonly full: string;
  readonly color: string;
};

export type MealRecord = Record<VariantKey, number> & {
  readonly t: string | null;
};

export type DayMeals = Record<MealKey, MealRecord>;

export type Group = {
  readonly id: string;
  readonly short: string;
  readonly name: string;
  readonly type: string;
  readonly headcount: number;
  readonly arrive: {
    readonly d: number;
    readonly m: number;
    readonly label: string;
  };
  readonly depart: {
    readonly d: number;
    readonly m: number;
    readonly label: string;
  };
  readonly contact: string;
  readonly color: string;
  readonly days: Partial<Record<DayKey, DayMeals>>;
  readonly note: string;
};

export const MEALS_NOW = {
  weekLabel: "Týden 18 · 2026",
  weekNo: 18,
  year: 2026,
  weekFrom: "27. dubna 2026",
  weekTo: "3. května 2026",
} as const;

export const WEEK_DAYS: readonly WeekDay[] = [
  {
    key: "mon",
    iso: "2026-04-27",
    dow: "Po",
    name: "Pondělí",
    d: 27,
    m: 4,
    label: "27. 4.",
  },
  {
    key: "tue",
    iso: "2026-04-28",
    dow: "Út",
    name: "Úterý",
    d: 28,
    m: 4,
    label: "28. 4.",
  },
  {
    key: "wed",
    iso: "2026-04-29",
    dow: "St",
    name: "Středa",
    d: 29,
    m: 4,
    label: "29. 4.",
    today: true,
  },
  {
    key: "thu",
    iso: "2026-04-30",
    dow: "Čt",
    name: "Čtvrtek",
    d: 30,
    m: 4,
    label: "30. 4.",
  },
  {
    key: "fri",
    iso: "2026-05-01",
    dow: "Pá",
    name: "Pátek",
    d: 1,
    m: 5,
    label: "1. 5.",
  },
  {
    key: "sat",
    iso: "2026-05-02",
    dow: "So",
    name: "Sobota",
    d: 2,
    m: 5,
    label: "2. 5.",
    weekend: true,
  },
  {
    key: "sun",
    iso: "2026-05-03",
    dow: "Ne",
    name: "Neděle",
    d: 3,
    m: 5,
    label: "3. 5.",
    weekend: true,
  },
];

export const MEAL_TYPES: readonly MealType[] = [
  { key: "S", label: "Snídaně", short: "S", icon: "pi-sun" },
  { key: "O", label: "Oběd", short: "O", icon: "pi-bowl-food" },
  { key: "OB", label: "Balíček", short: "OB", icon: "pi-briefcase" },
  { key: "V", label: "Večeře", short: "V", icon: "pi-moon" },
];

export const DIET_VARIANTS: readonly DietVariant[] = [
  { key: "N", label: "Normální", full: "Normální strava", color: "#52525b" },
  { key: "V", label: "Vegetariánské", full: "Vegetariánská", color: "#65a30d" },
  { key: "B", label: "Bezlepkové", full: "Bezlepková", color: "#d97706" },
  { key: "L", label: "Bezlaktózové", full: "Bezlaktózová", color: "#0891b2" },
  {
    key: "BL",
    label: "Bez lepku/laktózy",
    full: "Bezlepková a bezlaktózová",
    color: "#be185d",
  },
  {
    key: "BV",
    label: "Bezlep. veg.",
    full: "Bezlepková vegetariánská",
    color: "#ca8a04",
  },
  {
    key: "LV",
    label: "Bezlakt. veg.",
    full: "Bezlaktózová vegetariánská",
    color: "#0d9488",
  },
  {
    key: "BLV",
    label: "Bez lep./lakt. veg.",
    full: "Bezlepková, bezlaktózová a vegetariánská",
    color: "#7c3aed",
  },
];

const m = (
  N = 0,
  V = 0,
  B = 0,
  L = 0,
  t: string | null = null
): MealRecord => ({ N, V, B, L, BL: 0, BV: 0, LV: 0, BLV: 0, t });

const md = (
  S: MealRecord,
  O: MealRecord,
  V: MealRecord,
  OB: MealRecord = m()
): DayMeals => ({ S, O, OB, V });

export const GROUPS: readonly Group[] = [
  {
    id: "g-vut",
    short: "VUT",
    name: "VUT Brno · FIT — soustředění",
    type: "Vysoká škola",
    headcount: 26,
    arrive: { d: 22, m: 4, label: "st 22. 4." },
    depart: { d: 6, m: 5, label: "st 6. 5." },
    contact: "Doc. Vondráček · 605 118 442",
    color: "#7c3aed",
    days: {
      mon: md(
        m(22, 2, 1, 1, "07:30"),
        m(22, 2, 1, 1, "12:00"),
        m(22, 2, 1, 1, "18:00")
      ),
      tue: md(
        m(22, 2, 1, 1, "07:30"),
        m(22, 2, 1, 1, "12:00"),
        m(22, 2, 1, 1, "18:00")
      ),
      wed: md(
        m(22, 2, 1, 1, "07:30"),
        m(22, 2, 1, 1, "12:00"),
        m(22, 2, 1, 1, "18:00")
      ),
      thu: md(
        m(22, 2, 1, 1, "07:30"),
        m(22, 2, 1, 1, "12:00"),
        m(22, 2, 1, 1, "18:00")
      ),
      fri: md(
        m(22, 2, 1, 1, "07:30"),
        m(22, 2, 1, 1, "12:00"),
        m(22, 2, 1, 1, "18:00")
      ),
      sat: md(m(22, 2, 1, 1, "08:00"), m(22, 2, 1, 1, "12:30"), m(0, 0, 0, 0)),
      sun: md(
        m(22, 2, 1, 1, "08:00"),
        m(22, 2, 1, 1, "12:30"),
        m(22, 2, 1, 1, "18:30")
      ),
    },
    note: "Trvalá objednávka — bez jídla v sobotu večer (vlastní grilovačka).",
  },
  {
    id: "g-gym",
    short: "Gymnázium Třinec",
    name: "Gymnázium Třinec · 2.A",
    type: "Střední škola",
    headcount: 28,
    arrive: { d: 28, m: 4, label: "út 28. 4." },
    depart: { d: 5, m: 5, label: "út 5. 5." },
    contact: "Mgr. Kalousková · 723 884 119",
    color: "#0d9488",
    days: {
      tue: md(m(0, 0, 0, 0), m(24, 3, 0, 1, "12:30"), m(24, 3, 0, 1, "18:30")),
      wed: md(
        m(24, 3, 0, 1, "07:45"),
        m(24, 3, 0, 1, "12:30"),
        m(24, 3, 0, 1, "18:30")
      ),
      thu: md(
        m(24, 3, 0, 1, "07:45"),
        m(24, 3, 0, 1, "12:30"),
        m(24, 3, 0, 1, "18:30")
      ),
      fri: md(
        m(24, 3, 0, 1, "07:45"),
        m(24, 3, 0, 1, "12:30"),
        m(24, 3, 0, 1, "18:30")
      ),
      sat: md(
        m(24, 3, 0, 1, "08:15"),
        m(24, 3, 0, 1, "13:00"),
        m(24, 3, 0, 1, "19:00")
      ),
      sun: md(
        m(24, 3, 0, 1, "08:15"),
        m(24, 3, 0, 1, "13:00"),
        m(24, 3, 0, 1, "18:30")
      ),
    },
    note: "3 vegetariáni a 1 alergik na laktózu — potvrzeno e-mailem.",
  },
  {
    id: "g-mat",
    short: "Mateřinka",
    name: "MŠ Lipová — výlet pro starší děti",
    type: "Mateřská škola",
    headcount: 16,
    arrive: { d: 1, m: 5, label: "pá 1. 5." },
    depart: { d: 5, m: 5, label: "út 5. 5." },
    contact: "Bc. Tichá · 776 224 117",
    color: "#db2777",
    days: {
      fri: md(m(0, 0, 0, 0), m(0, 0, 0, 0), m(13, 0, 2, 1, "17:30")),
      sat: md(
        m(13, 0, 2, 1, "08:00"),
        m(13, 0, 2, 1, "11:30"),
        m(13, 0, 2, 1, "17:30")
      ),
      sun: md(
        m(13, 0, 2, 1, "08:00"),
        m(13, 0, 2, 1, "11:30"),
        m(13, 0, 2, 1, "17:30")
      ),
    },
    note: "Děti 5–6 let — menší porce. 2× bezlepková (celiakie), 1× bez laktózy.",
  },
  {
    id: "g-zsv",
    short: "ZŠ Vsetín",
    name: "ZŠ Vsetín-Sychrov · 7.B",
    type: "Základní škola",
    headcount: 22,
    arrive: { d: 27, m: 4, label: "po 27. 4." },
    depart: { d: 1, m: 5, label: "pá 1. 5." },
    contact: "Mgr. Brázda · 605 998 117",
    color: "#ea580c",
    days: {
      mon: md(m(0, 0, 0, 0), m(20, 1, 1, 0, "12:00"), m(20, 1, 1, 0, "18:00")),
      tue: md(
        m(20, 1, 1, 0, "07:30"),
        m(20, 1, 1, 0, "12:00"),
        m(20, 1, 1, 0, "18:00")
      ),
      wed: md(
        m(20, 1, 1, 0, "07:30"),
        m(20, 1, 1, 0, "12:00"),
        m(20, 1, 1, 0, "18:00")
      ),
      thu: md(
        m(20, 1, 1, 0, "07:30"),
        m(20, 1, 1, 0, "12:00"),
        m(20, 1, 1, 0, "18:00")
      ),
      fri: md(m(20, 1, 1, 0, "07:30"), m(0, 0, 0, 0), m(0, 0, 0, 0)),
    },
    note: "",
  },
  {
    id: "g-skv",
    short: "Skaut Valmez",
    name: "Skautský oddíl Valašské Meziříčí",
    type: "Volnočasová organizace",
    headcount: 18,
    arrive: { d: 30, m: 4, label: "čt 30. 4." },
    depart: { d: 3, m: 5, label: "ne 3. 5." },
    contact: "Ing. Kubíček · 723 117 884",
    color: "#0284c7",
    days: {
      thu: md(m(0, 0, 0, 0), m(0, 0, 0, 0), m(16, 2, 0, 0, "19:00")),
      fri: md(
        m(16, 2, 0, 0, "08:30"),
        m(16, 2, 0, 0, "13:00"),
        m(16, 2, 0, 0, "19:00")
      ),
      sat: md(
        m(16, 2, 0, 0, "08:30"),
        m(16, 2, 0, 0, "13:00"),
        m(16, 2, 0, 0, "19:00")
      ),
      sun: md(m(16, 2, 0, 0, "08:30"), m(16, 2, 0, 0, "12:30"), m(0, 0, 0, 0)),
    },
    note: "Polovina účastníků pod 15 let.",
  },
];

export type DayTotals = Record<MealKey, Record<VariantKey, number>>;
export type WeekTotals = {
  readonly totals: Record<DayKey, DayTotals>;
  readonly weekGrand: number;
};

export function computeTotals(groups: readonly Group[]): WeekTotals {
  const totals = {} as Record<DayKey, DayTotals>;
  let weekGrand = 0;
  for (const day of WEEK_DAYS) {
    const dayTotals = {} as DayTotals;
    for (const meal of MEAL_TYPES) {
      const variantTotals = {} as Record<VariantKey, number>;
      for (const variant of DIET_VARIANTS) {
        let s = 0;
        for (const g of groups) {
          const dd = g.days[day.key];
          if (!dd) {
            continue;
          }
          s += dd[meal.key][variant.key];
        }
        variantTotals[variant.key] = s;
        weekGrand += s;
      }
      dayTotals[meal.key] = variantTotals;
    }
    totals[day.key] = dayTotals;
  }
  return { totals, weekGrand };
}

export function sumVariants(rec: MealRecord | null | undefined): number {
  if (!rec) {
    return 0;
  }
  return DIET_VARIANTS.reduce((acc, v) => acc + rec[v.key], 0);
}

export function sumDayForGroup(group: Group, dayKey: DayKey): number {
  const dd = group.days[dayKey];
  if (!dd) {
    return 0;
  }
  return MEAL_TYPES.reduce((acc, mt) => acc + sumVariants(dd[mt.key]), 0);
}

export function sumWeekForGroup(group: Group): number {
  return WEEK_DAYS.reduce(
    (acc, day) => acc + sumDayForGroup(group, day.key),
    0
  );
}

export type EarliestPickup = {
  readonly time: string | null;
  readonly count: number;
  readonly portions: number;
};

export type EarliestByDay = Record<DayKey, Record<MealKey, EarliestPickup>>;

export function computeEarliest(groups: readonly Group[]): EarliestByDay {
  const out = {} as Record<DayKey, Record<MealKey, EarliestPickup>>;
  for (const day of WEEK_DAYS) {
    const perMeal = {} as Record<MealKey, EarliestPickup>;
    for (const meal of MEAL_TYPES) {
      let best: string | null = null;
      let count = 0;
      let portions = 0;
      for (const g of groups) {
        const dd = g.days[day.key];
        if (!dd) {
          continue;
        }
        const rec = dd[meal.key];
        const total = sumVariants(rec);
        if (total === 0) {
          continue;
        }
        count += 1;
        portions += total;
        if (rec.t && (best === null || rec.t < best)) {
          best = rec.t;
        }
      }
      perMeal[meal.key] = { time: best, count, portions };
    }
    out[day.key] = perMeal;
  }
  return out;
}

export const MEAL_API_KEYS = {
  S: "breakfast",
  O: "lunch",
  OB: "lunchPackage",
  V: "dinner",
} as const satisfies Record<
  MealKey,
  keyof Omit<MealResponse, "reservationId" | "date">
>;

export const VARIANT_API_KEYS = {
  N: "normal",
  V: "vegetarian",
  B: "glutenFree",
  L: "lactoseFree",
  BL: "glutenFreeLactoseFree",
  BV: "glutenFreeVegetarian",
  LV: "lactoseFreeVegetarian",
  BLV: "glutenFreeLactoseFreeVegetarian",
} as const satisfies Record<VariantKey, keyof Omit<MealAmountDto, "at">>;

const DAY_KEYS_BY_DOW: readonly DayKey[] = [
  "sun",
  "mon",
  "tue",
  "wed",
  "thu",
  "fri",
  "sat",
];

const CZ_DOW: readonly string[] = ["Ne", "Po", "Út", "St", "Čt", "Pá", "So"];
const CZ_DAY: readonly string[] = [
  "Neděle",
  "Pondělí",
  "Úterý",
  "Středa",
  "Čtvrtek",
  "Pátek",
  "Sobota",
];
const CZ_MONTH_GENITIVE: readonly string[] = [
  "ledna",
  "února",
  "března",
  "dubna",
  "května",
  "června",
  "července",
  "srpna",
  "září",
  "října",
  "listopadu",
  "prosince",
];

export function isoWeekStart(year: number, week: number): Date {
  const jan4 = new Date(Date.UTC(year, 0, 4));
  const dow = (jan4.getUTCDay() + 6) % 7;
  const week1Mon = new Date(jan4);
  week1Mon.setUTCDate(jan4.getUTCDate() - dow);
  const result = new Date(week1Mon);
  result.setUTCDate(week1Mon.getUTCDate() + (week - 1) * 7);
  return result;
}

export function isoWeekFor(date: Date): { year: number; week: number } {
  const d = new Date(
    Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate())
  );
  const dow = (d.getUTCDay() + 6) % 7;
  d.setUTCDate(d.getUTCDate() - dow + 3);
  const firstThu = new Date(Date.UTC(d.getUTCFullYear(), 0, 4));
  const week =
    1 + Math.round((d.getTime() - firstThu.getTime()) / 86400000 / 7);
  return { year: d.getUTCFullYear(), week };
}

export function addWeeks(
  year: number,
  week: number,
  delta: number
): { year: number; week: number } {
  const start = isoWeekStart(year, week);
  start.setUTCDate(start.getUTCDate() + delta * 7);
  return isoWeekFor(start);
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

function isoDate(d: Date): string {
  return `${d.getUTCFullYear()}-${pad2(d.getUTCMonth() + 1)}-${pad2(d.getUTCDate())}`;
}

function dayKeyFromDow(dow: number): DayKey {
  return DAY_KEYS_BY_DOW[dow] as DayKey;
}

export function weekDaysFor(
  year: number,
  week: number,
  today: Date = new Date()
): readonly WeekDay[] {
  const start = isoWeekStart(year, week);
  const todayIso = isoDate(
    new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()))
  );
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(start);
    d.setUTCDate(start.getUTCDate() + i);
    const dow = d.getUTCDay();
    const day = d.getUTCDate();
    const month = d.getUTCMonth() + 1;
    const isWeekend = dow === 0 || dow === 6;
    const iso = isoDate(d);
    const isToday = iso === todayIso;
    return {
      key: dayKeyFromDow(dow),
      iso,
      dow: CZ_DOW[dow] ?? "",
      name: CZ_DAY[dow] ?? "",
      d: day,
      m: month,
      label: `${day}. ${month}.`,
      ...(isToday ? { today: true } : {}),
      ...(isWeekend ? { weekend: true } : {}),
    };
  });
}

export function dayMetaFor(
  iso: string,
  today: Date = new Date()
): WeekDay | null {
  const parts = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!parts) {
    return null;
  }
  const [, yStr, mStr, dStr] = parts;
  const y = Number(yStr);
  const month = Number(mStr);
  const day = Number(dStr);
  const date = new Date(Date.UTC(y, month - 1, day));
  const dow = date.getUTCDay();
  const todayIso = isoDate(
    new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()))
  );
  const isWeekend = dow === 0 || dow === 6;
  const isToday = iso === todayIso;
  return {
    key: dayKeyFromDow(dow),
    iso,
    dow: CZ_DOW[dow] ?? "",
    name: CZ_DAY[dow] ?? "",
    d: day,
    m: month,
    label: `${day}. ${month}.`,
    ...(isToday ? { today: true } : {}),
    ...(isWeekend ? { weekend: true } : {}),
  };
}

export function czLongDate(iso: string): string {
  const [y, m, d] = iso.split("-");
  const year = Number(y);
  const month = Number(m);
  const day = Number(d);
  const name = CZ_MONTH_GENITIVE[month - 1] ?? "";
  return `${day}. ${name} ${year}`;
}

const GROUP_PALETTE: readonly string[] = [
  "#7c3aed",
  "#0d9488",
  "#db2777",
  "#ea580c",
  "#0284c7",
  "#65a30d",
  "#d97706",
  "#be185d",
  "#0891b2",
  "#52525b",
];

function hashColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i += 1) {
    hash = (hash << 5) - hash + id.charCodeAt(i);
    hash |= 0;
  }
  return GROUP_PALETTE[Math.abs(hash) % GROUP_PALETTE.length] ?? "#52525b";
}

function dateLabel(iso: string): { d: number; m: number; label: string } {
  const [, mPart, dPart] = iso.split("-");
  const day = Number(dPart);
  const month = Number(mPart);
  const dow = (new Date(`${iso}T00:00:00Z`).getUTCDay() + 6) % 7;
  const dowLabel = CZ_DOW[(dow + 1) % 7] ?? "";
  return {
    d: day,
    m: month,
    label: `${dowLabel.toLowerCase()} ${day}. ${month}.`,
  };
}

export function emptyMealAmount(): MealAmountDto {
  return {
    at: null,
    normal: 0,
    glutenFree: 0,
    lactoseFree: 0,
    vegetarian: 0,
    glutenFreeLactoseFree: 0,
    glutenFreeVegetarian: 0,
    lactoseFreeVegetarian: 0,
    glutenFreeLactoseFreeVegetarian: 0,
  };
}

function recordFromAmount(amount: MealAmountDto): MealRecord {
  return {
    N: amount.normal,
    V: amount.vegetarian,
    B: amount.glutenFree,
    L: amount.lactoseFree,
    BL: amount.glutenFreeLactoseFree,
    BV: amount.glutenFreeVegetarian,
    LV: amount.lactoseFreeVegetarian,
    BLV: amount.glutenFreeLactoseFreeVegetarian,
    t: amount.at ? amount.at.slice(0, 5) : null,
  };
}

export function amountFromRecord(rec: MealRecord): MealAmountDto {
  return {
    at: rec.t,
    normal: rec.N,
    vegetarian: rec.V,
    glutenFree: rec.B,
    lactoseFree: rec.L,
    glutenFreeLactoseFree: rec.BL,
    glutenFreeVegetarian: rec.BV,
    lactoseFreeVegetarian: rec.LV,
    glutenFreeLactoseFreeVegetarian: rec.BLV,
  };
}

export function buildReplaceRequest(
  group: Group,
  dayKey: DayKey,
  isoDate: string
): ReplaceMealRequest {
  const day = group.days[dayKey];
  const recOrEmpty = (key: MealKey): MealAmountDto =>
    day ? amountFromRecord(day[key]) : emptyMealAmount();
  return {
    date: isoDate,
    breakfast: recOrEmpty("S"),
    lunch: recOrEmpty("O"),
    lunchPackage: recOrEmpty("OB"),
    dinner: recOrEmpty("V"),
  };
}

export function buildGroupsFromApi(
  meals: readonly MealResponse[],
  reservations: readonly Reservation[],
  weekDays: readonly WeekDay[]
): readonly Group[] {
  const dayKeyByIso = new Map<string, DayKey>(
    weekDays.map(wd => [wd.iso, wd.key])
  );

  const byReservation = new Map<string, MealResponse[]>();
  for (const meal of meals) {
    if (!dayKeyByIso.has(meal.date)) {
      continue;
    }
    const list = byReservation.get(meal.reservationId) ?? [];
    list.push(meal);
    byReservation.set(meal.reservationId, list);
  }

  const reservationById = new Map<string, Reservation>(
    reservations.map(r => [r.id, r])
  );

  const groups: Group[] = [];
  for (const [reservationId, entries] of byReservation) {
    const reservation = reservationById.get(reservationId);
    const days: Partial<Record<DayKey, DayMeals>> = {};
    let headcount = 0;
    for (const entry of entries) {
      const dayKey = dayKeyByIso.get(entry.date);
      if (!dayKey) {
        continue;
      }
      const dm: DayMeals = {
        S: recordFromAmount(entry.breakfast),
        O: recordFromAmount(entry.lunch),
        OB: recordFromAmount(entry.lunchPackage),
        V: recordFromAmount(entry.dinner),
      };
      days[dayKey] = dm;
      headcount = Math.max(
        headcount,
        sumVariants(dm.S),
        sumVariants(dm.O),
        sumVariants(dm.OB),
        sumVariants(dm.V)
      );
    }

    const short = reservation?.number ?? reservationId.slice(0, 6);
    const fullName = reservation
      ? `${reservation.reservationMakerName} ${reservation.reservationMakerSurname}`.trim()
      : short;
    const customLabel = reservation?.displayName?.trim() ?? "";
    const arriveLabel = reservation ? dateLabel(reservation.from) : null;
    const departLabel = reservation ? dateLabel(reservation.to) : null;

    groups.push({
      id: reservationId,
      short,
      name: customLabel || fullName || short,
      type: "",
      headcount,
      arrive: arriveLabel ?? { d: 0, m: 0, label: "—" },
      depart: departLabel ?? { d: 0, m: 0, label: "—" },
      contact: reservation
        ? `${fullName} · ${reservation.reservationMakerPhone}`.trim()
        : "",
      color: hashColor(reservationId),
      days,
      note: reservation?.note ?? "",
    });
  }

  groups.sort((a, b) => a.short.localeCompare(b.short, "cs"));
  return groups;
}
