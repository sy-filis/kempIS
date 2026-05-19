import type { ServiceGroup } from "./service-groups";

export type Language = {
  id: string;
  code: string;
  name: string;
};

export type LanguageRequest = {
  code: string;
  name: string;
};

export type VatRate = {
  id: string;
  name: string;
  rate: number;
  isActive: boolean;
};

export type VatRateRequest = {
  name: string;
  rate: number;
  isActive: boolean;
};

export type ServiceType = {
  id: string;
  name: string;
  isActive: boolean;
};

export type ServiceTypeRequest = {
  name: string;
  isActive: boolean;
};

export type Service = {
  id: string;
  serviceGroup: ServiceGroup;
  serviceTypeId: string;
  vatRateId: string;
  name: string;
  basePrice: number;
  isActive: boolean;
};

export type ServiceRequest = Omit<Service, "id">;

export type ServiceText = {
  id: string;
  serviceId: string;
  languageId: string;
  printText: string;
};

export type ServiceTextRequest = Omit<ServiceText, "id">;

export type CatalogueSpotGroup = {
  id: string;
  serviceId: string;
  name: string;
  description: string | null;
  capacity: number;
  isActive: boolean;
  imageUrl: string;
  detailsUrl: string;
};

export type SpotGroupRequest = Omit<CatalogueSpotGroup, "id">;

export type CatalogueSpot = {
  id: string;
  spotGroupId: string;
  name: string;
  description: string | null;
  isActive: boolean;
};

export type SpotRequest = Omit<CatalogueSpot, "id">;

export type Nationality = {
  id: string;
  name: string;
  nameEn: string;
  alpha2: string;
  alpha3: string;
  numeric: string;
  visaRequired: boolean;
  biometricsRequired: boolean;
  isEu: boolean;
  languageId: string;
  languageCode: string;
};

export type NationalityRequest = Omit<Nationality, "id" | "languageCode">;
