import {
  type AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  inject,
  signal,
  viewChild,
} from "@angular/core";

import { ReceptionTabletService } from "../reception-tablet.service";

type DetectorMessage =
  | { readonly type: "detected"; readonly value: string }
  | { readonly type: "unsupported" }
  | { readonly type: "error"; readonly message: string };

const QR_DETECT_INTERVAL_MS = 300;

@Component({
  selector: "kemp-is-tablet-scan-pair",
  standalone: true,
  imports: [],
  templateUrl: "./scan-pair.page.html",
  styleUrl: "./scan-pair.page.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScanPairPage implements AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly tablet = inject(ReceptionTabletService);

  protected readonly cameraError = signal<string | null>(null);

  private readonly videoRef =
    viewChild.required<ElementRef<HTMLVideoElement>>("video");

  private stream: MediaStream | null = null;
  private worker: Worker | null = null;
  private interval: ReturnType<typeof setInterval> | null = null;
  private busy = false;
  private settled = false;

  ngAfterViewInit(): void {
    void this.startCamera();
    this.destroyRef.onDestroy(() => this.teardown());
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
      await video.play().catch(() => undefined);
      this.startDetection();
    } catch (err) {
      this.cameraError.set(this.toCameraError(err));
    }
  }

  private startDetection(): void {
    this.worker = new Worker(
      new URL("../qr-detector.worker", import.meta.url),
      { type: "module" }
    );
    this.worker.addEventListener(
      "message",
      (event: MessageEvent<DetectorMessage>) => this.onMessage(event.data)
    );
    this.interval = setInterval(
      () => void this.sendFrame(),
      QR_DETECT_INTERVAL_MS
    );
  }

  private async sendFrame(): Promise<void> {
    if (this.busy || !this.worker) {
      return;
    }
    const video = this.videoRef().nativeElement;
    if (video.readyState < 2) {
      return;
    }
    this.busy = true;
    try {
      const bitmap = await createImageBitmap(video);
      this.worker.postMessage({ type: "frame", bitmap }, [bitmap]);
    } catch {
      // Skip this frame.
    } finally {
      this.busy = false;
    }
  }

  private onMessage(data: DetectorMessage): void {
    if (data.type === "detected") {
      this.settleWith(data.value);
    } else if (data.type === "unsupported") {
      this.stopInterval();
    }
  }

  private settleWith(pairCode: string): void {
    if (this.settled) {
      return;
    }
    this.settled = true;
    this.teardown();
    this.tablet.connect(pairCode);
  }

  private stopInterval(): void {
    if (this.interval) {
      clearInterval(this.interval);
      this.interval = null;
    }
  }

  private teardown(): void {
    this.stopInterval();
    this.worker?.terminate();
    this.worker = null;
    if (this.stream) {
      for (const t of this.stream.getTracks()) {
        t.stop();
      }
      this.stream = null;
    }
  }

  private toCameraError(err: unknown): string {
    if (err instanceof DOMException) {
      if (err.name === "NotAllowedError") {
        return $localize`:@@tablet.scan.cameraDenied:Přístup ke kameře byl zamítnut.`;
      }
      if (err.name === "NotFoundError" || err.name === "OverconstrainedError") {
        return $localize`:@@tablet.scan.cameraMissing:Kamera nebyla nalezena.`;
      }
    }
    return $localize`:@@tablet.scan.cameraGeneric:Kameru se nepodařilo spustit.`;
  }
}
