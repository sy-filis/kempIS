import {
  computed,
  DestroyRef,
  inject,
  Injectable,
  signal,
} from "@angular/core";
import { Router } from "@angular/router";

import { firstValueFrom } from "rxjs";

import { AuthApi } from "./auth.api";
import type {
  AuthBroadcast,
  CurrentUser,
  LoginVerifyResponse,
} from "./auth.types";
import { isWebAuthnSupported, requestAssertion } from "./passkey";

const CHANNEL_NAME = "kempis-staff-auth";
const STORAGE_KEY = "kempis-staff-auth";
const REFRESH_LOCK_NAME = "kempis-staff-auth-refresh";
const REFRESH_LEAD_MS = 30_000;
const PEER_REQUEST_WAIT_MS = 150;

type PersistedSession = {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly accessExpiresAt: number;
};

function loadPersisted(): PersistedSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === null) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (
      typeof parsed !== "object" ||
      parsed === null ||
      typeof (parsed as PersistedSession).accessToken !== "string" ||
      typeof (parsed as PersistedSession).refreshToken !== "string" ||
      typeof (parsed as PersistedSession).accessExpiresAt !== "number"
    ) {
      return null;
    }
    return parsed as PersistedSession;
  } catch {
    return null;
  }
}

function persist(session: PersistedSession): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // Storage may be disabled (private mode, quota); fall back to memory.
  }
}

function clearPersisted(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // noop
  }
}

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly channel = new BroadcastChannel(CHANNEL_NAME);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(null);
  private readonly _accessExpiresAt = signal<number | null>(null);
  private readonly _currentUser = signal<CurrentUser | null>(null);

  readonly accessToken = this._accessToken.asReadonly();
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => {
    const token = this._accessToken();
    const exp = this._accessExpiresAt();
    return token !== null && exp !== null && Date.now() < exp;
  });

  hasAnyRole(roles: readonly string[]): boolean {
    const user = this._currentUser();
    if (user === null) {
      return false;
    }
    return user.roles.some(r => roles.includes(r));
  }

  /** Resolves once `currentUser` is non-null or the load has been
   *  attempted. Used by guards that fire before `loadCurrentUser()`. */
  async ensureCurrentUserLoaded(): Promise<void> {
    if (this._currentUser() !== null) {
      return;
    }
    await this.loadCurrentUser();
  }

  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private refreshing: Promise<void> | null = null;
  private loadingUser: Promise<void> | null = null;

  constructor() {
    this.channel.addEventListener("message", e => {
      this.onPeerMessage(e as MessageEvent<AuthBroadcast>);
    });
    inject(DestroyRef).onDestroy(() => {
      this.cancelRefreshTimer();
      this.channel.close();
    });
  }

  /** Hydrates from `localStorage` first (so a reload skips the peer-tab
   *  handshake), then falls back to asking any open peer tab for an
   *  active session. As a final fallback, probes `/auth/me` to detect a
   *  backend running in --no-auth mode and adopts the synthetic identity.
   *  Wired via `provideAppInitializer` so the guard sees adopted tokens
   *  on the very first navigation. */
  async bootstrap(): Promise<void> {
    // The reception tablet PWA is anonymous by design — it never talks
    // to authenticated endpoints, only to the public Socket.IO server
    // via its single-use pair code. Skipping bootstrap here avoids the
    // /auth/me probe (and any peer-tab token adoption) on the tablet
    // surface even when another tab on the same device has a staff
    // session in localStorage.
    if (
      typeof location !== "undefined" &&
      location.pathname.startsWith("/reception-tablet")
    ) {
      return;
    }
    if (this.isAuthenticated()) {
      return;
    }
    const persisted = loadPersisted();
    if (persisted !== null) {
      this._accessToken.set(persisted.accessToken);
      this._refreshToken.set(persisted.refreshToken);
      this._accessExpiresAt.set(persisted.accessExpiresAt);
      if (Date.now() >= persisted.accessExpiresAt) {
        // Access token expired; the refresh token may still be valid.
        await this.refresh();
      } else {
        this.scheduleRefresh(persisted.accessExpiresAt - Date.now());
        void this.loadCurrentUser();
      }
      return;
    }
    this.channel.postMessage({ type: "request" } satisfies AuthBroadcast);
    await new Promise<void>(resolve =>
      setTimeout(resolve, PEER_REQUEST_WAIT_MS)
    );
    if (this.isAuthenticated()) {
      return;
    }
    await this.tryAdoptNoAuthSession();
  }

  // Backend in --no-auth mode lets /auth/me succeed anonymously. Probe once
  // and adopt the synthetic identity so the operator can bootstrap without
  // a passkey. Persists nothing; a real login replaces the placeholder.
  private async tryAdoptNoAuthSession(): Promise<void> {
    try {
      const user = await firstValueFrom(this.api.getMe());
      this._currentUser.set(user);
      this._accessToken.set("no-auth");
      const expiresAt =
        user.sessionExpiresAt !== null
          ? Date.parse(user.sessionExpiresAt)
          : Number.MAX_SAFE_INTEGER;
      this._accessExpiresAt.set(expiresAt);
    } catch {
      // 401 - backend is not in no-auth mode; route guard will send the
      // user to /staff/login as usual.
    }
  }

  async loginWithPasskey(): Promise<void> {
    if (!isWebAuthnSupported()) {
      throw new Error("WEBAUTHN_UNSUPPORTED");
    }
    const options = await firstValueFrom(this.api.getLoginChallenge());
    const assertion = await requestAssertion(options);
    const tokens = await firstValueFrom(
      this.api.verifyLogin(JSON.stringify(assertion))
    );
    this.applyTokens(tokens);
  }

  refresh(): Promise<void> {
    if (this.refreshing !== null) {
      return this.refreshing;
    }
    this.refreshing = this.doRefresh().finally(() => {
      this.refreshing = null;
    });
    return this.refreshing;
  }

  async logout(): Promise<void> {
    const rt = this._refreshToken();
    this.cancelRefreshTimer();
    this.clearState();
    this.channel.postMessage({ type: "logout" } satisfies AuthBroadcast);
    if (rt !== null) {
      try {
        await firstValueFrom(this.api.logout(rt));
      } catch {
        // Best-effort. Backend may be unreachable; client state is already cleared.
      }
    }
    void this.router.navigateByUrl("/staff/login");
  }

  /** Serializes refresh across tabs via the Web Locks API. The
   *  single-use refresh token means concurrent `/auth/refresh` calls
   *  from sibling tabs would all 401 except the winner. Inside the
   *  lock we re-read `localStorage` so a tab that queued behind the
   *  winner adopts the freshly-rotated session instead of replaying
   *  its now-spent refresh token. */
  private async doRefresh(): Promise<void> {
    const prevExp = this._accessExpiresAt();
    await navigator.locks.request(REFRESH_LOCK_NAME, async () => {
      const persisted = loadPersisted();
      if (persisted === null) {
        await this.logout();
        return;
      }
      if (prevExp !== null && persisted.accessExpiresAt > prevExp) {
        this.adoptSession(persisted);
        return;
      }
      try {
        const tokens = await firstValueFrom(
          this.api.refresh(persisted.refreshToken)
        );
        this.applyTokens(tokens);
      } catch {
        await this.logout();
      }
    });
  }

  private applyTokens(tokens: LoginVerifyResponse): void {
    const expiresAt = Date.now() + tokens.expiresIn * 1000;
    this._accessToken.set(tokens.accessToken);
    this._refreshToken.set(tokens.refreshToken);
    this._accessExpiresAt.set(expiresAt);
    persist({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      accessExpiresAt: expiresAt,
    });
    this.channel.postMessage({
      type: "session",
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      accessExpiresAt: expiresAt,
    } satisfies AuthBroadcast);
    this.scheduleRefresh(tokens.expiresIn * 1000);
    void this.loadCurrentUser();
  }

  private loadCurrentUser(): Promise<void> {
    if (this.loadingUser !== null) {
      return this.loadingUser;
    }
    this.loadingUser = (async (): Promise<void> => {
      try {
        const user = await firstValueFrom(this.api.getMe());
        this._currentUser.set(user);
      } catch {
        this._currentUser.set(null);
      }
    })().finally(() => {
      this.loadingUser = null;
    });
    return this.loadingUser;
  }

  private scheduleRefresh(ttlMs: number): void {
    this.cancelRefreshTimer();
    const delay = Math.max(0, ttlMs - REFRESH_LEAD_MS);
    this.refreshTimer = setTimeout(() => {
      void this.refresh();
    }, delay);
  }

  private cancelRefreshTimer(): void {
    if (this.refreshTimer !== null) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  private adoptSession(persisted: PersistedSession): void {
    this._accessToken.set(persisted.accessToken);
    this._refreshToken.set(persisted.refreshToken);
    this._accessExpiresAt.set(persisted.accessExpiresAt);
    persist(persisted);
    this.cancelRefreshTimer();
    const ttl = persisted.accessExpiresAt - Date.now();
    if (ttl > 0) {
      this.scheduleRefresh(ttl);
    }
    void this.loadCurrentUser();
  }

  private clearState(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    this._accessExpiresAt.set(null);
    this._currentUser.set(null);
    clearPersisted();
  }

  private onPeerMessage(event: MessageEvent<AuthBroadcast>): void {
    const msg = event.data;
    switch (msg.type) {
      case "session":
        this.adoptSession({
          accessToken: msg.accessToken,
          refreshToken: msg.refreshToken,
          accessExpiresAt: msg.accessExpiresAt,
        });
        break;
      case "logout":
        this.cancelRefreshTimer();
        this.clearState();
        if (this.router.url.startsWith("/staff/auth")) {
          void this.router.navigateByUrl("/staff/login");
        }
        break;
      case "request": {
        const at = this._accessToken();
        const rt = this._refreshToken();
        const exp = this._accessExpiresAt();
        if (at !== null && rt !== null && exp !== null && Date.now() < exp) {
          this.channel.postMessage({
            type: "session",
            accessToken: at,
            refreshToken: rt,
            accessExpiresAt: exp,
          } satisfies AuthBroadcast);
        }
        break;
      }
    }
  }
}
