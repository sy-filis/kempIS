export type User = {
  id: string;
  username: string;
  name: string;
  roles: readonly string[];
  isDisabled: boolean;
  createdAtUtc: number;
};

export type UserDetail = User & {
  passkeyCount: number;
};

export type CreateUserRequest = {
  username: string;
  name: string;
  role: string;
};

export type CreateUserResponse = {
  id: string;
};

export type UpdateUserRequest = {
  username: string;
  name: string;
  roles: readonly string[];
};

export type Passkey = {
  id: string;
  displayName: string | null;
  createdAtUtc: number;
};
