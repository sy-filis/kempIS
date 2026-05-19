/// <reference lib="webworker" />

import { toPlateCandidate } from "./plate-format";

type DetectedTextRegion = {
  readonly rawValue: string;
};

type TextDetectorInstance = {
  detect(image: ImageBitmapSource): Promise<DetectedTextRegion[]>;
};

type TextDetectorCtor = new () => TextDetectorInstance;

type FrameMessage = { readonly type: "frame"; readonly bitmap: ImageBitmap };

type OutMessage =
  | { readonly type: "detected"; readonly value: string }
  | { readonly type: "unsupported" }
  | { readonly type: "error"; readonly message: string };

const ctor = (globalThis as unknown as { TextDetector?: TextDetectorCtor })
  .TextDetector;

if (!ctor) {
  postOut({ type: "unsupported" });
} else {
  const detector = new ctor();

  self.addEventListener("message", (event: MessageEvent) => {
    if (!isFrameMessage(event.data)) {
      return;
    }
    const bitmap = event.data.bitmap;
    detector
      .detect(bitmap)
      .then(regions => {
        for (const region of regions) {
          const candidate = toPlateCandidate(region.rawValue);
          if (candidate) {
            postOut({ type: "detected", value: candidate });
            return;
          }
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
