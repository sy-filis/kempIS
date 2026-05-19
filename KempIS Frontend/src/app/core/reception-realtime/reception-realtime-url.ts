import { inject } from "@angular/core";

import { API_BASE_URL } from "../api/api-base-url.token";

/** Builds the URL for the reception realtime WebSocket endpoint. The
 *  endpoint lives under the same Kestrel host/port as the HTTP API, at
 *  `<apiBaseUrl>/reception/realtime`. The connection itself is anonymous
 *  (authorization happens via the pair code emitted on `pair:join`), so
 *  the builder takes no parameters. Must be called from an injection
 *  context. */
export function injectReceptionRealtimeUrlBuilder(): () => string {
  const apiBase = inject(API_BASE_URL);
  const parsed = new URL(apiBase);
  const proto = parsed.protocol === "https:" ? "wss:" : "ws:";
  const basePath = parsed.pathname.replace(/\/$/, "");

  return (): string => `${proto}//${parsed.host}${basePath}/reception/realtime`;
}
