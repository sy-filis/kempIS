import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from "@angular/common/http";
import type { ApplicationConfig } from "@angular/core";
import {
  inject,
  isDevMode,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from "@angular/core";
import { provideAnimationsAsync } from "@angular/platform-browser/animations/async";
import { provideRouter, withComponentInputBinding } from "@angular/router";
import { provideServiceWorker } from "@angular/service-worker";

import Aura from "@primeng/themes/aura";
import { PrimeNG } from "primeng/config";
import { providePrimeNG } from "primeng/config";

import { routes } from "./app.routes";
import { environment } from "../environments/environment";
import { API_BASE_URL } from "./core/api/api-base-url.token";
import { httpErrorInterceptor } from "./core/api/http-error.interceptor";
import { authTokenInterceptor } from "./core/auth/auth-token.interceptor";
import { AuthService } from "./core/auth/auth.service";
import { passkeyCredentialsInterceptor } from "./core/auth/passkey-credentials.interceptor";
import { CAMP_IDENTITY } from "./core/camp/camp-identity.token";
import { PRIMENG_TRANSLATIONS_CS } from "./shared/primeng-translations.cs";
import { PRIMENG_TRANSLATIONS_EN } from "./shared/primeng-translations.en";

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(
      withFetch(),
      withInterceptors([
        passkeyCredentialsInterceptor,
        authTokenInterceptor,
        httpErrorInterceptor,
      ])
    ),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: { preset: Aura, options: { darkModeSelector: false } },
      translation: PRIMENG_TRANSLATIONS_CS,
    }),
    provideAppInitializer(() => {
      const locale = inject(LOCALE_ID);
      if (locale.startsWith("en")) {
        inject(PrimeNG).setTranslation(PRIMENG_TRANSLATIONS_EN);
      }
    }),
    provideAppInitializer(() => inject(AuthService).bootstrap()),
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: CAMP_IDENTITY, useValue: environment.camp },
    provideServiceWorker("ngsw-worker.js", {
      enabled: !isDevMode(),
      registrationStrategy: "registerWhenStable:30000",
    }),
  ],
};
