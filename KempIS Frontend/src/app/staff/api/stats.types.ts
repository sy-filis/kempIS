export type GuestStatsByCountryResponse = {
  from: string;
  to: string;
  totalGuests: number;
  totalPersonNights: number;
  rows: readonly GuestStatsByCountryRow[];
};

export type GuestStatsByCountryRow = {
  nationalityId: string;
  alpha2: string;
  alpha3: string;
  name: string;
  nameEn: string;
  guestCount: number;
  personNights: number;
};

export type ServiceRevenueStatsResponse = {
  from: string;
  to: string;
  totalNet: number;
  totalVat: number;
  totalGross: number;
  groups: readonly ServiceRevenueGroup[];
};

export type ServiceRevenueGroup = {
  serviceGroup: string;
  groupNet: number;
  groupVat: number;
  groupGross: number;
  services: readonly ServiceRevenueRow[];
};

export type ServiceRevenueRow = {
  serviceId: string;
  serviceName: string;
  isActive: boolean;
  vatRatePercentage: number;
  quantity: number;
  net: number;
  vat: number;
  gross: number;
};

export type OccupancyStatsResponse = {
  from: string;
  to: string;
  nightsInRange: number;
  totalOccupiedSpotNights: number;
  totalCapacitySpotNights: number;
  totalOccupancyPercent: number;
  groups: readonly OccupancyStatsRow[];
};

export type OccupancyStatsRow = {
  spotGroupId: string;
  name: string;
  isActive: boolean;
  capacity: number;
  occupiedSpotNights: number;
  capacitySpotNights: number;
  occupancyPercent: number;
};

export type RevenueByPaymentMethodResponse = {
  from: string;
  to: string;
  totalBillCount: number;
  totalGross: number;
  rows: readonly RevenueByPaymentMethodRow[];
};

export type RevenueByPaymentMethodRow = {
  paymentType: string;
  billCount: number;
  gross: number;
  sharePercent: number;
};
