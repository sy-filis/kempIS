import type { Environment } from "./environment.type";

export const environment: Environment = {
  apiBaseUrl: "https://kempis.zukalovi.eu:5001/api",
  camp: {
    name: "Kemp Olšovec",
    phoneDisplay: "+420 725 896 488",
    phoneTel: "+420725896488",
    email: "rezervace@olsovec.cz",
    address: {
      street: "Havlíčkovo nám. 71",
      city: "Jedovnice",
      zip: "67906",
    },
  },
  skipAuth: false,
};
