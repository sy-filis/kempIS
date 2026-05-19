/// <reference lib="webworker" />

type BarcodeDetectorOptions = {
  formats?: readonly string[];
};

type DetectedBarcode = {
  readonly rawValue: string;
};

type BarcodeDetectorCtor = new (options?: BarcodeDetectorOptions) => {
  detect(image: ImageBitmapSource): Promise<DetectedBarcode[]>;
};

type FrameMessage = { readonly type: "frame"; readonly bitmap: ImageBitmap };

type OutMessage =
  | { readonly type: "detected"; readonly value: string }
  | { readonly type: "unsupported" }
  | { readonly type: "error"; readonly message: string };

const ctor = (
  globalThis as unknown as { BarcodeDetector?: BarcodeDetectorCtor }
).BarcodeDetector;

if (!ctor) {
  postOut({ type: "unsupported" });
} else {
  const detector = new ctor({ formats: ["qr_code"] });

  self.addEventListener("message", (event: MessageEvent) => {
    if (!isFrameMessage(event.data)) {
      return;
    }
    const bitmap = event.data.bitmap;
    detector
      .detect(bitmap)
      .then(results => {
        const first = results[0];
        if (first) {
          postOut({ type: "detected", value: first.rawValue });
        }
      })
      .catch((err: unknown) => {
        postOut({
          type: "error",
          message: err instanceof Error ? err.message : String(err),
        });
      })
      .finally(() => {
        bitmap.close();
      });
  });
}

function isFrameMessage(data: unknown): data is FrameMessage {
  return (
    typeof data === "object" &&
    data !== null &&
    (data as { type?: unknown }).type === "frame"
  );
}

function postOut(message: OutMessage): void {
  (self as DedicatedWorkerGlobalScope).postMessage(message);
}
