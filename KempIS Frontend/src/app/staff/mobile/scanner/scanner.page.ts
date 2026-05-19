import type { AfterViewInit, ElementRef } from "@angular/core";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
  viewChild,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ButtonModule } from "primeng/button";
import { InputTextModule } from "primeng/inputtext";

import { formatPlate } from "./plate-format";
import { VehiclesApi } from "../../api/vehicles.api";
import { ScreenHeader } from "../shared/screen-header";

type ScanMode = "plate" | "qr";

type ScanStatus = "pending" | "matched" | "unmatched" | "duplicate";

type ScanSource = "camera" | "manual";

type ScanEntry = {
  readonly id: string;
  readonly mode: ScanMode;
  readonly value: string;
  readonly timestamp: number;
  readonly status: ScanStatus;
  readonly source: ScanSource;
  readonly variableSymbol?: string;
  readonly guestName?: string;
  readonly spotId?: string;
  readonly note?: string;
  // ISO YYYY-MM-DD; only set when status === "matched".
  readonly checkoutAt?: string;
};

type DetectorMessage =
  | { readonly type: "detected"; readonly value: string }
  | { readonly type: "unsupported" }
  | { readonly type: "error"; readonly message: string };

type DetectorRuntime = {
  worker: Worker | null;
  interval: ReturnType<typeof setInterval> | null;
  busy: boolean;
};

const PLATE_DETECT_INTERVAL_MS = 500;
const QR_DETECT_INTERVAL_MS = 250;
const DEDUPE_WINDOW_MS = 6_000;
const HISTORY_LIMIT = 50;
const CLOCK_TICK_MS = 30_000;

@Component({
  selector: "kemp-is-staff-scanner",
  imports: [FormsModule, ButtonModule, InputTextModule, ScreenHeader],
  templateUrl: "./scanner.page.html",
  styleUrl: "./scanner.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScannerPage implements AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly vehiclesApi = inject(VehiclesApi);

  protected readonly mode = signal<ScanMode>("plate");
  protected readonly manualValue = signal("");
  protected readonly cameraError = signal<string | null>(null);
  protected readonly detectorError = signal<string | null>(null);
  protected readonly history = signal<readonly ScanEntry[]>([]);

  private readonly cameraReady = signal(false);
  private readonly clock = signal(Date.now());

  protected readonly hasHistory = computed(() => this.history().length > 0);

  private readonly videoRef =
    viewChild.required<ElementRef<HTMLVideoElement>>("video");

  private stream: MediaStream | null = null;

  private readonly plate: DetectorRuntime = {
    worker: null,
    interval: null,
    busy: false,
  };
  private readonly qr: DetectorRuntime = {
    worker: null,
    interval: null,
    busy: false,
  };

  private readonly lastSeen = new Map<string, number>();

  public constructor() {
    effect(() => {
      if (!this.cameraReady()) {
        return;
      }
      const m = this.mode();
      this.stopAllIntervals();
      this.detectorError.set(null);
      if (m === "plate") {
        this.startDetection("plate");
      } else {
        this.startDetection("qr");
      }
    });

    const tick = setInterval(() => this.clock.set(Date.now()), CLOCK_TICK_MS);
    this.destroyRef.onDestroy(() => clearInterval(tick));
  }

  public ngAfterViewInit(): void {
    void this.startCamera();
    this.destroyRef.onDestroy(() => this.teardown());
  }

  protected setMode(value: ScanMode): void {
    this.mode.set(value);
  }

  protected onManualSubmit(): void {
    const raw = this.manualValue().trim();
    if (!raw) {
      return;
    }
    const value =
      this.mode() === "plate" ? formatPlate(raw) : raw.toUpperCase();
    this.recordDetection(this.mode(), value, "manual");
    this.manualValue.set("");
  }

  protected clearHistory(): void {
    this.history.set([]);
    this.lastSeen.clear();
  }

  protected statusLabel(status: ScanStatus): string {
    switch (status) {
      case "pending":
        return "Čeká na ověření";
      case "matched":
        return "Rezervace nalezena";
      case "unmatched":
        return "Bez účtenky";
      case "duplicate":
        return "Duplicitní sken";
    }
  }

  protected formatCheckout(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
    if (!m) {
      return iso;
    }
    return `${Number(m[3])}. ${Number(m[2])}.`;
  }

  protected formatRelative(ts: number): string {
    const diffSec = Math.max(0, Math.floor((this.clock() - ts) / 1000));
    if (diffSec < 30) {
      return "Právě teď";
    }
    if (diffSec < 60) {
      return "Před chvílí";
    }
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) {
      return `Před ${diffMin} min`;
    }
    const diffHr = Math.floor(diffMin / 60);
    return `Před ${diffHr} h`;
  }

  private async startCamera(): Promise<void> {
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: "environment" },
          width: { ideal: 1280 },
          height: { ideal: 720 },
        },
        audio: false,
      });
      const video = this.videoRef().nativeElement;
      video.srcObject = this.stream;
      await video.play().catch(() => {
        // Some browsers require a user gesture before play; muted/inline video
        // makes this rare. Leave the stream attached; next tap will resume.
      });
      this.cameraReady.set(true);
    } catch (err) {
      this.cameraError.set(this.toCameraError(err));
    }
  }

  private startDetection(mode: ScanMode): void {
    const runtime = mode === "plate" ? this.plate : this.qr;
    if (!runtime.worker) {
      runtime.worker =
        mode === "plate"
          ? new Worker(new URL("./plate-detector.worker", import.meta.url), {
              type: "module",
            })
          : new Worker(new URL("./qr-detector.worker", import.meta.url), {
              type: "module",
            });
      runtime.worker.addEventListener(
        "message",
        (event: MessageEvent<DetectorMessage>) =>
          this.onDetectorMessage(mode, event.data)
      );
    }
    const intervalMs =
      mode === "plate" ? PLATE_DETECT_INTERVAL_MS : QR_DETECT_INTERVAL_MS;
    runtime.interval = setInterval(() => void this.sendFrame(mode), intervalMs);
  }

  private onDetectorMessage(mode: ScanMode, data: DetectorMessage): void {
    if (data.type === "detected") {
      this.recordDetection(mode, data.value, "camera");
    } else if (data.type === "unsupported") {
      this.detectorError.set(
        mode === "plate"
          ? "Skener SPZ není v tomto prohlížeči podporován - použijte ruční zadání."
          : "Skener QR není v tomto prohlížeči podporován - použijte ruční zadání."
      );
      this.stopInterval(mode);
    }
    // 'error' messages ignored; usually a transient frame issue.
  }

  private async sendFrame(mode: ScanMode): Promise<void> {
    const runtime = mode === "plate" ? this.plate : this.qr;
    if (runtime.busy || !runtime.worker) {
      return;
    }
    const video = this.videoRef().nativeElement;
    if (video.readyState < 2) {
      return;
    }
    runtime.busy = true;
    try {
      const bitmap = await createImageBitmap(video);
      runtime.worker.postMessage({ type: "frame", bitmap }, [bitmap]);
    } catch {
      // Skip this frame.
    } finally {
      runtime.busy = false;
    }
  }

  private stopAllIntervals(): void {
    this.stopInterval("plate");
    this.stopInterval("qr");
  }

  private stopInterval(mode: ScanMode): void {
    const runtime = mode === "plate" ? this.plate : this.qr;
    if (runtime.interval) {
      clearInterval(runtime.interval);
      runtime.interval = null;
    }
  }

  private teardown(): void {
    this.stopAllIntervals();
    for (const runtime of [this.plate, this.qr]) {
      if (runtime.worker) {
        runtime.worker.terminate();
        runtime.worker = null;
      }
    }
    if (this.stream) {
      for (const track of this.stream.getTracks()) {
        track.stop();
      }
      this.stream = null;
    }
  }

  private recordDetection(
    mode: ScanMode,
    value: string,
    source: ScanSource
  ): void {
    if (this.history().some(e => e.mode === mode && e.value === value)) {
      return;
    }

    const key = `${mode}:${value}`;
    const now = Date.now();
    const last = this.lastSeen.get(key) ?? 0;
    if (now - last < DEDUPE_WINDOW_MS) {
      return;
    }
    this.lastSeen.set(key, now);

    const id = `${now}-${Math.random().toString(36).slice(2, 8)}`;
    const entry: ScanEntry = {
      id,
      mode,
      value,
      timestamp: now,
      status: "pending",
      source,
    };

    this.history.update(items => [entry, ...items].slice(0, HISTORY_LIMIT));

    if (mode === "plate") {
      this.lookupPlate(id, value);
    }
  }

  private lookupPlate(entryId: string, plate: string): void {
    this.vehiclesApi.lookup({ licencePlate: plate }).subscribe({
      next: response => {
        this.updateEntry(entryId, {
          status: "matched",
          checkoutAt: response.checkoutAt,
        });
      },
      error: () => {
        this.updateEntry(entryId, { status: "unmatched" });
      },
    });
  }

  private updateEntry(id: string, patch: Partial<ScanEntry>): void {
    this.history.update(items =>
      items.map(item => (item.id === id ? { ...item, ...patch } : item))
    );
  }

  private toCameraError(err: unknown): string {
    if (err instanceof DOMException) {
      if (err.name === "NotAllowedError") {
        return "Přístup ke kameře byl zamítnut.";
      }
      if (err.name === "NotFoundError" || err.name === "OverconstrainedError") {
        return "Nebyla nalezena žádná zadní kamera.";
      }
    }
    return "Kameru se nepodařilo spustit.";
  }
}
