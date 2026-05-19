import { inject, Injectable, LOCALE_ID } from "@angular/core";

export type AppLocale = "cs" | "en";

const SUPPORTED_LOCALES: readonly AppLocale[] = ["cs", "en"];
const DEFAULT_LOCALE: AppLocale = "cs";

/** Localhost dev ports - must match angular.json `serve` configurations. */
const DEV_PORT_BY_LOCALE: Record<AppLocale, number> = {
  cs: 4200,
  en: 4201,
};

/** Production: cs at `/`, en under `/en/` (server maps each subpath to
 *  its locale bundle). Localhost dev: each locale runs on its own
 *  port (cs:4200, en:4201) and both serve at root - `ng serve`
 *  doesn't honor `servePath` for `<base href>`, so dev paths stay
 *  unprefixed. */
export function computeOtherLocaleUrl(
  target: AppLocale,
  origin: string,
  pathname: string,
  search: string,
  hash: string
): string {
  const stripped = stripEnPrefix(pathname);
  if (isLocalhost(origin)) {
    const targetOrigin = setPort(origin, DEV_PORT_BY_LOCALE[target]);
    return `${targetOrigin}${stripped}${search}${hash}`;
  }
  const newPath =
    target === "en" ? `/en${stripped === "/" ? "/" : stripped}` : stripped;
  return `${newPath}${search}${hash}`;
}

function isLocalhost(origin: string): boolean {
  return /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(origin);
}

function setPort(origin: string, port: number): string {
  return origin.replace(/:\d+$/, "").concat(`:${port}`);
}

function stripEnPrefix(pathname: string): string {
  if (pathname === "/en" || pathname === "/en/") {
    return "/";
  }
  return pathname.startsWith("/en/") ? pathname.slice(3) : pathname;
}

@Injectable({ providedIn: "root" })
export class LocaleService {
  private readonly localeId = inject(LOCALE_ID);

  readonly currentLocale: AppLocale = SUPPORTED_LOCALES.includes(
    this.localeId as AppLocale
  )
    ? (this.localeId as AppLocale)
    : DEFAULT_LOCALE;

  switchTo(target: AppLocale): void {
    if (target === this.currentLocale) {
      return;
    }
    const url = computeOtherLocaleUrl(
      target,
      window.location.origin,
      window.location.pathname,
      window.location.search,
      window.location.hash
    );
    window.location.assign(url);
  }
}
