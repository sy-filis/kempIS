import {
  DestroyRef,
  inject,
  Injectable,
  signal,
  type Signal,
} from "@angular/core";

@Injectable({ providedIn: "root" })
export class RefreshController {
  private readonly _tick = signal(0);
  private readonly _lastRefreshAt = signal<Date>(new Date());
  private readonly _now = signal<number>(Date.now());

  readonly tick: Signal<number> = this._tick.asReadonly();
  readonly lastRefreshAt: Signal<Date> = this._lastRefreshAt.asReadonly();
  readonly now: Signal<number> = this._now.asReadonly();

  private refreshIntervalId: ReturnType<typeof setInterval>;
  private readonly nowIntervalId: ReturnType<typeof setInterval>;

  constructor() {
    this.refreshIntervalId = setInterval(() => this.bumpTick(), 60_000);
    this.nowIntervalId = setInterval(() => this._now.set(Date.now()), 1_000);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(this.refreshIntervalId);
      clearInterval(this.nowIntervalId);
    });
  }

  refreshNow(): void {
    clearInterval(this.refreshIntervalId);
    this.refreshIntervalId = setInterval(() => this.bumpTick(), 60_000);
    this.bumpTick();
  }

  private bumpTick(): void {
    this._tick.update(n => n + 1);
    this._lastRefreshAt.set(new Date());
  }
}

export function formatRelativeAgo(nowMs: number, lastRefreshAt: Date): string {
  const elapsedSec = Math.max(
    0,
    Math.floor((nowMs - lastRefreshAt.getTime()) / 1000)
  );
  if (elapsedSec < 5) {
    return "Právě teď";
  }
  if (elapsedSec < 60) {
    return `Před ${elapsedSec} s`;
  }
  const totalMin = Math.floor(elapsedSec / 60);
  if (totalMin < 60) {
    return `Před ${totalMin} min`;
  }
  const hours = Math.floor(totalMin / 60);
  const minutes = totalMin % 60;
  return minutes === 0 ? `Před ${hours} h` : `Před ${hours} h ${minutes} min`;
}
