import { inject, Injectable, signal } from "@angular/core";

import { firstValueFrom } from "rxjs";

import { EdokladyApi } from "./edoklady.api";
import type { VirtualServiceCounter } from "./edoklady.types";
import { isApiError } from "../api/api-error";

const STORAGE_KEY = "kemp-is.edoklady.counter-id";

function readStoredId(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStoredId(id: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, id);
  } catch {
    // Storage may be disabled (private mode, quota); fall back to memory.
  }
}

function clearStoredId(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // noop
  }
}

/** Singleton store for the camp's virtual service counter. Resolves
 *  lazily (get-or-create), persists the id in `localStorage` so it is
 *  reused across sessions. */
@Injectable({ providedIn: "root" })
export class EdokladyCounterStore {
  private readonly api = inject(EdokladyApi);

  private readonly _counter = signal<VirtualServiceCounter | null>(null);
  readonly counter = this._counter.asReadonly();

  private readonly _ensuring = signal<boolean>(false);
  readonly ensuring = this._ensuring.asReadonly();

  /** Single-flight guard for concurrent `ensureCounter()` calls. */
  private inFlight: Promise<VirtualServiceCounter> | null = null;

  /** Returns the cached counter if present; otherwise fetches the one
   *  identified by `localStorage`, falling back to creating a fresh
   *  one if the stored id is missing or returns 404. */
  ensureCounter(): Promise<VirtualServiceCounter> {
    const cached = this._counter();
    if (cached) {
      return Promise.resolve(cached);
    }
    if (this.inFlight) {
      return this.inFlight;
    }
    this._ensuring.set(true);
    this.inFlight = this.resolveOrCreate()
      .then(counter => {
        this._counter.set(counter);
        return counter;
      })
      .finally(() => {
        this.inFlight = null;
        this._ensuring.set(false);
      });
    return this.inFlight;
  }

  /** Re-fetches the counter, bypassing the in-memory cache. No-op
   *  while a resolve is already in flight. */
  refreshCounter(): Promise<VirtualServiceCounter> {
    if (this.inFlight) {
      return this.inFlight;
    }
    this._counter.set(null);
    return this.ensureCounter();
  }

  /** Drops the stored counter so the next `ensureCounter()` POSTs a
   *  fresh one. No-op while a resolve is in flight. */
  reset(): void {
    if (this.inFlight) {
      return;
    }
    clearStoredId();
    this._counter.set(null);
  }

  private async resolveOrCreate(): Promise<VirtualServiceCounter> {
    const storedId = readStoredId();
    if (storedId) {
      try {
        return await firstValueFrom(this.api.getCounter(storedId));
      } catch (error) {
        // Stored id is stale (counter deleted upstream) - drop and create.
        if (isApiError(error) && error.status === 404) {
          clearStoredId();
        } else {
          throw error;
        }
      }
    }
    const created = await firstValueFrom(this.api.createCounter(null));
    writeStoredId(created.id);
    return created;
  }
}
