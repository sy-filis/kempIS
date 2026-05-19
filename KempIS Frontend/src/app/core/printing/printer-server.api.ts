import { HttpClient, HttpHeaders } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";

import type { Observable } from "rxjs";

/** Local print-server (KempISPrintService) client. The server URL is
 *  supplied per-call because it is user-configured at runtime. */
@Injectable({ providedIn: "root" })
export class PrinterServerApi {
  private readonly http = inject(HttpClient);

  listPrinters(serverUrl: string): Observable<string[]> {
    return this.http.get<string[]>(joinUrl(serverUrl, "/api/v1/printers"));
  }

  printPdf(
    serverUrl: string,
    printerName: string,
    pdf: Blob
  ): Observable<void> {
    const url = joinUrl(
      serverUrl,
      `/api/v1/printers/${encodeURIComponent(printerName)}`
    );
    return this.http.post<void>(url, pdf, {
      headers: new HttpHeaders({ "Content-Type": "application/pdf" }),
    });
  }
}

function joinUrl(base: string, path: string): string {
  const trimmedBase = base.replace(/\/+$/, "");
  const prefixedPath = path.startsWith("/") ? path : `/${path}`;
  return `${trimmedBase}${prefixedPath}`;
}
