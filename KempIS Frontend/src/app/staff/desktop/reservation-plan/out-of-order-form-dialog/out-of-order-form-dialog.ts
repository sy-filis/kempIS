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
import { MessageModule } from "primeng/message";
import { TextareaModule } from "primeng/textarea";
import { TreeSelectModule } from "primeng/treeselect";
import type { Observable } from "rxjs";

import { ApiClient } from "../../../../core/api/api-client";
import type {
  OutOfOrder,
  OutOfOrderRequest,
} from "../../../api/out-of-orders.types";
import type { Spot, SpotGroup } from "../../../api/spots.types";

@Component({
  selector: "kemp-is-out-of-order-form-dialog",
  imports: [
    FormsModule,
    ButtonModule,
    ChipModule,
    ConfirmDialogModule,
    DatePickerModule,
    DialogModule,
    MessageModule,
    PrimeTemplate,
    TextareaModule,
    TreeSelectModule,
  ],
  providers: [ConfirmationService],
  templateUrl: "./out-of-order-form-dialog.html",
  styleUrl: "./out-of-order-form-dialog.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OutOfOrderFormDialog {
  private readonly apiClient = inject(ApiClient);
  private readonly confirmService = inject(ConfirmationService);

  constructor() {
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const o = this.outOfOrder();
      if (!o) {
        this.reset();
        return;
      }
      this.fromDate.set(parseLocalDate(o.from));
      this.toDate.set(parseLocalDate(o.to));
      this.reason.set(o.reason);
      this.errorMessage.set(null);

      const leafByKey = new Map<string, TreeNode>();
      const parentByKey = new Map<string, TreeNode>();
      for (const group of this.treeNodes()) {
        if (typeof group.key === "string") {
          parentByKey.set(group.key, group);
        }
        for (const child of group.children ?? []) {
          if (typeof child.key === "string") {
            leafByKey.set(child.key, child);
          }
        }
      }
      const selected: TreeNode[] = [];
      for (const groupId of o.spotGroupIds) {
        const parent = parentByKey.get(`group:${groupId}`);
        if (parent) {
          selected.push(parent);
          for (const child of parent.children ?? []) {
            selected.push(child);
          }
        }
      }
      for (const spotId of o.spotIds) {
        const leaf = leafByKey.get(spotId);
        if (leaf && !selected.includes(leaf)) {
          selected.push(leaf);
        }
      }
      this.treeSelection.set(selected);
    });
  }

  readonly visible = model<boolean>(false);
  readonly outOfOrder = input<OutOfOrder | null>(null);
  readonly spotGroups = input.required<readonly SpotGroup[]>();
  readonly spots = input.required<readonly Spot[]>();

  readonly mutated = output<"created" | "updated" | "deleted">();

  protected readonly mode = computed<"create" | "edit">(() =>
    this.outOfOrder() ? "edit" : "create"
  );

  protected readonly fromDate = signal<Date | null>(null);
  protected readonly toDate = signal<Date | null>(null);
  protected readonly reason = signal<string>("");
  protected readonly treeSelection = signal<TreeNode[]>([]);

  protected readonly submitting = signal<boolean>(false);
  protected readonly errorMessage = signal<string | null>(null);

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

  protected readonly selectedSpotGroupIds = computed<string[]>(() =>
    this.treeSelection()
      .map(n => n.key)
      .filter(
        (k): k is string => typeof k === "string" && k.startsWith("group:")
      )
      .map(k => k.slice("group:".length))
  );

  protected readonly selectedSpotIds = computed<string[]>(() => {
    const fullGroupIds = new Set(this.selectedSpotGroupIds());
    const spotById = new Map(this.spots().map(s => [s.id, s] as const));
    return this.treeSelection()
      .map(n => n.key)
      .filter(
        (k): k is string => typeof k === "string" && !k.startsWith("group:")
      )
      .filter(spotId => {
        const spot = spotById.get(spotId);
        return !spot || !fullGroupIds.has(spot.spotGroupId);
      });
  });

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
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t || f.getTime() > t.getTime()) {
      return false;
    }
    if (
      this.selectedSpotIds().length + this.selectedSpotGroupIds().length ===
      0
    ) {
      return false;
    }
    if (this.reason().trim().length === 0) {
      return false;
    }
    return true;
  });

  protected readonly canDelete = computed<boolean>(
    () => this.mode() === "edit"
  );

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
    const f = this.fromDate();
    const t = this.toDate();
    if (!f || !t) {
      return;
    }

    const body: OutOfOrderRequest = {
      from: toLocalDateString(f),
      to: toLocalDateString(t),
      reason: this.reason().trim(),
      spotGroupIds: this.selectedSpotGroupIds(),
      spotIds: this.selectedSpotIds(),
    };

    this.submitting.set(true);
    this.errorMessage.set(null);

    const existing = this.outOfOrder();
    const request$: Observable<string | void> = existing
      ? this.apiClient.put<void>(`/out-of-orders/${existing.id}`, body)
      : this.apiClient.post<string>("/out-of-orders", body);

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        this.mutated.emit(existing ? "updated" : "created");
        this.visible.set(false);
        this.reset();
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  protected onDelete(): void {
    const existing = this.outOfOrder();
    if (!existing || this.submitting()) {
      return;
    }
    this.confirmService.confirm({
      message:
        "Opravdu chcete smazat tento záznam mimo provoz? Tato operace je nevratná.",
      header: "Smazat záznam",
      icon: "pi pi-exclamation-triangle",
      acceptLabel: "Smazat",
      rejectLabel: "Zpět",
      acceptButtonStyleClass: "p-button-danger",
      accept: () => {
        this.submitting.set(true);
        this.errorMessage.set(null);
        this.apiClient.delete<void>(`/out-of-orders/${existing.id}`).subscribe({
          next: () => {
            this.submitting.set(false);
            this.mutated.emit("deleted");
            this.visible.set(false);
            this.reset();
          },
          error: () => {
            this.submitting.set(false);
            this.errorMessage.set("Záznam se nepodařilo smazat.");
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
      this.errorMessage.set("Vybrané chaty mají v tomto termínu kolizi.");
      return;
    }
    if (status === 400) {
      this.errorMessage.set("Zkontrolujte vyplněné údaje.");
      return;
    }
    if (status === 404) {
      this.errorMessage.set("Záznam již neexistuje, načtěte seznam znovu.");
      return;
    }
    this.errorMessage.set("Operace se nezdařila. Zkuste to prosím znovu.");
  }

  private reset(): void {
    this.submitting.set(false);
    this.fromDate.set(null);
    this.toDate.set(null);
    this.reason.set("");
    this.treeSelection.set([]);
    this.errorMessage.set(null);
  }
}

function toLocalDateString(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function parseLocalDate(s: string): Date {
  const [y, m, d] = s.split("-").map(Number) as [number, number, number];
  return new Date(y, m - 1, d);
}
