import {
  computed,
  inject,
  Injectable,
  signal,
  type Signal,
} from "@angular/core";

import { firstValueFrom } from "rxjs";

import {
  PRINT_COPIES_MAX,
  PRINT_COPIES_MIN,
  PRINT_TASK_DEFAULT_COPIES,
  PRINT_TASK_IDS,
  type PrintTaskId,
} from "./print-task";
import { PrinterServerApi } from "./printer-server.api";
import { isApiError } from "../api/api-error";

const DEFAULT_SERVER_URL = "http://127.0.0.1:9000";
const SERVER_URL_KEY = "kemp-is.printing.server-url";
const DEFAULT_KEY_PREFIX = "kemp-is.printing.default.";
const COPIES_KEY_PREFIX = "kemp-is.printing.copies.";

type Defaults = Readonly<Record<PrintTaskId, string | null>>;
type CopiesDefaults = Readonly<Record<PrintTaskId, number>>;

function readString(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeString(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage may be disabled (private mode, quota); fall back to memory.
  }
}

function removeKey(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // noop
  }
}

function readDefaults(): Defaults {
  const out = {} as Record<PrintTaskId, string | null>;
  for (const task of PRINT_TASK_IDS) {
    out[task] = readString(`${DEFAULT_KEY_PREFIX}${task}`);
  }
  return out;
}

function clampCopies(value: number): number {
  if (!Number.isFinite(value)) {
    return PRINT_COPIES_MIN;
  }
  const rounded = Math.trunc(value);
  if (rounded < PRINT_COPIES_MIN) {
    return PRINT_COPIES_MIN;
  }
  if (rounded > PRINT_COPIES_MAX) {
    return PRINT_COPIES_MAX;
  }
  return rounded;
}

function readCopiesDefaults(): CopiesDefaults {
  const out = {} as Record<PrintTaskId, number>;
  for (const task of PRINT_TASK_IDS) {
    const raw = readString(`${COPIES_KEY_PREFIX}${task}`);
    const parsed = raw === null ? Number.NaN : Number.parseInt(raw, 10);
    out[task] = Number.isFinite(parsed)
      ? clampCopies(parsed)
      : PRINT_TASK_DEFAULT_COPIES[task];
  }
  return out;
}

/** Per-workstation printer settings. Persistent state lives in
 *  `localStorage` under `kemp-is.printing.*`. */
@Injectable({ providedIn: "root" })
export class PrinterSettingsStore {
  private readonly api = inject(PrinterServerApi);

  private readonly _serverUrl = signal<string>(
    readString(SERVER_URL_KEY) ?? DEFAULT_SERVER_URL
  );
  readonly serverUrl = this._serverUrl.asReadonly();

  private readonly _printers = signal<readonly string[]>([]);
  readonly printers = this._printers.asReadonly();

  private readonly _loading = signal<boolean>(false);
  readonly loading = this._loading.asReadonly();

  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  private readonly _defaults = signal<Defaults>(readDefaults());
  readonly defaults = this._defaults.asReadonly();

  private readonly _copiesDefaults =
    signal<CopiesDefaults>(readCopiesDefaults());
  readonly copiesDefaults = this._copiesDefaults.asReadonly();

  /** Single-flight guard for concurrent refreshPrinters() calls. */
  private inFlight: Promise<readonly string[]> | null = null;

  defaultFor(task: PrintTaskId): Signal<string | null> {
    return computed(() => this._defaults()[task]);
  }

  copiesFor(task: PrintTaskId): Signal<number> {
    return computed(() => this._copiesDefaults()[task]);
  }

  async setServerUrl(url: string): Promise<void> {
    const trimmed = url.trim();
    if (trimmed === this._serverUrl()) {
      return;
    }
    if (trimmed === "") {
      removeKey(SERVER_URL_KEY);
    } else {
      writeString(SERVER_URL_KEY, trimmed);
    }
    this._serverUrl.set(trimmed);
    this._printers.set([]);
    this._error.set(null);
    this.inFlight = null;
    if (trimmed !== "") {
      await this.refreshPrinters();
    }
  }

  refreshPrinters(): Promise<readonly string[]> {
    if (this.inFlight) {
      return this.inFlight;
    }
    const url = this._serverUrl();
    if (url === "") {
      return Promise.resolve([]);
    }
    this._loading.set(true);
    this._error.set(null);
    this.inFlight = firstValueFrom(this.api.listPrinters(url))
      .then(list => {
        if (this._serverUrl() !== url) {
          return this._printers();
        }
        this._printers.set([...list]);
        return this._printers();
      })
      .catch((err: unknown) => {
        if (this._serverUrl() !== url) {
          return this._printers();
        }
        this._error.set(messageFor(err));
        this._printers.set([]);
        return this._printers();
      })
      .finally(() => {
        this.inFlight = null;
        this._loading.set(false);
      });
    return this.inFlight;
  }

  setDefaultFor(task: PrintTaskId, printer: string | null): void {
    const next = { ...this._defaults(), [task]: printer };
    this._defaults.set(next);
    if (printer === null || printer === "") {
      removeKey(`${DEFAULT_KEY_PREFIX}${task}`);
    } else {
      writeString(`${DEFAULT_KEY_PREFIX}${task}`, printer);
    }
  }

  setCopiesFor(task: PrintTaskId, copies: number): void {
    const clamped = clampCopies(copies);
    const next = { ...this._copiesDefaults(), [task]: clamped };
    this._copiesDefaults.set(next);
    if (clamped === PRINT_TASK_DEFAULT_COPIES[task]) {
      removeKey(`${COPIES_KEY_PREFIX}${task}`);
    } else {
      writeString(`${COPIES_KEY_PREFIX}${task}`, String(clamped));
    }
  }

  dismissError(): void {
    this._error.set(null);
  }
}

function messageFor(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 0) {
      return "Tiskový server není dostupný.";
    }
    return `Tiskový server vrátil chybu (${err.status}).`;
  }
  return "Tiskový server není dostupný.";
}
