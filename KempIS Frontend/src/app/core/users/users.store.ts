import { httpResource } from "@angular/common/http";
import { computed, inject, Injectable } from "@angular/core";

import type { User } from "./users.types";
import { ApiClient } from "../api/api-client";

@Injectable({ providedIn: "root" })
export class UsersStore {
  private readonly apiClient = inject(ApiClient);

  readonly users = httpResource<readonly User[]>(() =>
    this.apiClient.url("/users")
  );

  readonly byId = computed<ReadonlyMap<string, User>>(() => {
    const list = this.users.hasValue() ? this.users.value() : [];
    return new Map(list.map(u => [u.id, u]));
  });

  user(id: string | null | undefined): User | undefined {
    if (!id) {
      return undefined;
    }
    return this.byId().get(id);
  }

  name(id: string | null | undefined): string | null {
    return this.user(id)?.name ?? null;
  }
}
