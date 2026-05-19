import { Roles } from "./roles";

export function defaultLandingFor(roles: readonly string[]): string {
  if (roles.includes(Roles.CleaningStaff) && roles.length === 1) {
    return "/staff/auth/mobile";
  }
  if (roles.includes(Roles.Manager) || roles.includes(Roles.Receptionist)) {
    return "/staff/auth/desktop/dashboard";
  }
  if (roles.includes(Roles.Accountant)) {
    return "/staff/auth/desktop/bills";
  }
  return "/staff/login";
}
