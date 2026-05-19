import { httpResource } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";

import { ConfirmationService, PrimeTemplate } from "primeng/api";
import type { TreeNode } from "primeng/api";
import { ButtonModule } from "primeng/button";
import { ChipModule } from "primeng/chip";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { DatePickerModule } from "primeng/datepicker";
import { DialogModule } from "primeng/dialog";
import { InputTextModule } from "primeng/inputtext";
import { MessageModule } from "primeng/message";
import { TextareaModule } from "primeng/textarea";
import { TreeSelectModule } from "primeng/treeselect";
import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import { dateToIso, isoToDate } from "../../../../shared/date-iso";
import {
  type GroupReservationDetail,
  type GroupReservationRequest,
  GroupReservationState,
} from "../../../api/group-reservations.types";
import type { Spot, SpotGroup } from "../../../api/spots.types";

const EMAIL_RE = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

@Component({
  selector: "kemp-is-group-reservation-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    ChipModule,
    ConfirmDialogModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    PrimeTemplate,
    TextareaModule,
    TreeSelectModule,
  ],
  providers: [ConfirmationService],
  templateUrl: "./group-reservation-form-dialog.html",
  styleUrl: "./group-reservation-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupReservationFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly confirmService = inject(ConfirmationService);

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      if (this.mode() === "create") {
        this.reset();
        return;
      }
      if (!this.detail.hasValue()) {
        return;
      }
      const d = this.detail.value();
      this.from.set(isoToDate(d.from));
      this.to.set(isoToDate(d.to));
      this.organizerName.set(d.organizerName);
      this.organizerEmail.set(d.organizerEmail);
      this.organizerPhone.set(d.organizerPhone);
      this.note.set(d.note ?? "");
      this.displayName.set(d.displayName ?? "");
      this.errorMessage.set(null);

      const leafByKey = new Map<string, TreeNode>();
      for (const group of this.treeNodes()) {
        for (const child of group.children ?? []) {
          if (typeof child.key === "string") {
            leafByKey.set(child.key, child);
          }
        }
      }
      const selected: TreeNode[] = [];
      for (const spotId of d.spotIds) {
        const leaf = leafByKey.get(spotId);
        if (leaf) {
          selected.push(leaf);
        }
      }
      this.treeSelection.set(selected);
    });
  }

  readonly visible = model<boolean>(false);
  readonly groupReservationId = input<string | null>(null);
  readonly spotGroups = input.required<readonly SpotGroup[]>();
  readonly spots = input.required<readonly Spot[]>();

  readonly mutated = output<"created" | "updated" | "canceled">();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.groupReservationId() ? "edit" : "create"
  );

  protected readonly from = signal<Date | null>(null);
  protected readonly to = signal<Date | null>(null);
  protected readonly organizerName = signal<string>("");
  protected readonly organizerEmail = signal<string>("");
  protected readonly organizerPhone = signal<string>("");
  protected readonly note = signal<string>("");
  protected readonly displayName = signal<string>("");
  protected readonly treeSelection = signal<TreeNode[]>([]);

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly detail = httpResource<GroupReservationDetail>(() => {
    const id = this.groupReservationId();
    return id && this.visible()
      ? this.apiClient.url(`/group-reservations/${id}`)
      : undefined;
  });

  protected readonly treeNodes = computed<TreeNode[]>(() => {
    const byGroup = new Map<string, Spot[]>();
    for (const s of this.spots()) {
      if (!s.isActive) {
        continue;
      }
      const list = byGroup.get(s.spotGroupId) ?? [];
      list.push(s);
      byGroup.set(s.spotGroupId, list);
    }
    return [...this.spotGroups()]
      .filter(g => (byGroup.get(g.id) ?? []).length > 0)
      .sort((a, b) => a.name.localeCompare(b.name, "cs"))
      .map(g => ({
        key: `group:${g.id}`,
        label: g.name,
        selectable: true,
        children: (byGroup.get(g.id) ?? [])
          .sort((a, b) => a.name.localeCompare(b.name, "cs", { numeric: true }))
          .map(s => ({ key: s.id, label: s.name, leaf: true })),
      }));
  });

  protected readonly selectedSpotIds = computed<string[]>(() =>
    this.treeSelection()
      .filter(n => !n.children || n.children.length === 0)
      .map(n => n.key)
      .filter(
        (k): k is string => typeof k === "string" && !k.startsWith("group:")
      )
  );

  protected readonly selectedSpotChips = computed<
    { readonly key: string; readonly label: string }[]
  >(() =>
    this.treeSelection()
      .filter(n => !n.children || n.children.length === 0)
      .map(n => ({
        key: typeof n.key === "string" ? n.key : "",
        label: n.label ?? "",
      }))
      .filter(c => !c.key.startsWith("group:"))
      .sort((a, b) => a.label.localeCompare(b.label, "cs", { numeric: true }))
  );

  protected readonly canSubmit = computed<boolean>(() => {
    if (this.submitting()) {
      return false;
    }
    const f = this.from();
    const t = this.to();
    if (!f || !t || f.getTime() > t.getTime()) {
      return false;
    }
    if (this.selectedSpotIds().length === 0) {
      return false;
    }
    if (this.organizerName().trim().length === 0) {
      return false;
    }
    if (!EMAIL_RE.test(this.organizerEmail().trim())) {
      return false;
    }
    if (this.organizerPhone().trim().length === 0) {
      return false;
    }
    return true;
  });

  protected readonly canCancel = computed<boolean>(() => {
    if (this.mode() !== "edit") {
      return false;
    }
    if (!this.detail.hasValue()) {
      return false;
    }
    return this.detail.value().state !== GroupReservationState.Canceled;
  });

  protected onVisibleChange(visible: boolean): void {
    this.visible.set(visible);
    if (!visible) {
      this.reset();
    }
  }

  protected onCancel(): void {
    if (this.submitting()) {
      return;
    }
    this.visible.set(false);
    this.reset();
  }

  protected onSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }
    const f = this.from();
    const t = this.to();
    if (!f || !t) {
      return;
    }

    const noteText = this.note().trim();
    const displayNameText = this.displayName().trim();
    const body: GroupReservationRequest = {
      from: dateToIso(f),
      to: dateToIso(t),
      spotIds: this.selectedSpotIds(),
      organizerName: this.organizerName().trim(),
      organizerEmail: this.organizerEmail().trim(),
      organizerPhone: this.organizerPhone().trim(),
      note: noteText.length > 0 ? noteText : null,
      displayName: displayNameText.length > 0 ? displayNameText : null,
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    const id = this.groupReservationId();
    const request$: Observable<string | void> = id
      ? this.apiClient.put<void>(`/group-reservations/${id}`, body)
      : this.apiClient.post<string>("/group-reservations", body);

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        this.mutated.emit(id ? "updated" : "created");
        this.visible.set(false);
        this.reset();
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  protected onCancelReservation(): void {
    const id = this.groupReservationId();
    if (!id || this.submitting()) {
      return;
    }
    this.confirmService.confirm({
      message:
        "Opravdu chcete zrušit tuto skupinovou rezervaci? Tato operace je nevratná.",
      header: "Zrušit skupinovou rezervaci",
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Zrušit rezervaci",
      rejectLabel: "Zpět",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.submitting.set(true);
        this.errorMessage.set(null);
        this.apiClient
          .post<void>(`/group-reservations/${id}/cancel`, {})
          .subscribe({
            next: () => {
              this.submitting.set(false);
              this.mutated.emit("canceled");
              this.visible.set(false);
              this.reset();
            },
            error: () => {
              this.submitting.set(false);
              this.errorMessage.set("Rezervaci se nepodařilo zrušit.");
            },
          });
      },
    });
  }

  private handleError(err: unknown): void {
    this.submitting.set(false);
    const status =
      typeof err === "object" && err !== null && "status" in err
        ? (err as { status: unknown }).status
        : null;
    if (status === 409) {
      this.errorMessage.set("Vybrané chaty jsou v zadaném termínu obsazené.");
      return;
    }
    if (status === 400) {
      this.errorMessage.set("Zkontrolujte vyplněné údaje.");
      return;
    }
    if (status === 404) {
      this.errorMessage.set("Rezervace již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Operace se nezdařila. Zkuste to prosím znovu.");
  }

  private reset(): void {
    this.from.set(null);
    this.to.set(null);
    this.organizerName.set("");
    this.organizerEmail.set("");
    this.organizerPhone.set("");
    this.note.set("");
    this.displayName.set("");
    this.treeSelection.set([]);
    this.errorMessage.set(null);
  }
}
