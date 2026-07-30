"use client";

import { useEffect, useState } from "react";
import {
  getSelectedCity,
  getSelectedLocality,
  subscribeToLocationChanges,
  type SelectedLocality,
} from "@/lib/location";
import type { City } from "@/lib/types";

/**
 * The customer's chosen city/locality, reactive to changes made anywhere in
 * the app (SRS 11.1 - "customer should be able to change city from homepage").
 *
 * `undefined` until the first client render: localStorage does not exist
 * during SSR, and guessing either way would flash the wrong catalog before
 * correcting itself.
 */
export function useSelectedCity(): {
  city: City | null | undefined;
  locality: SelectedLocality | null;
} {
  const [city, setCity] = useState<City | null | undefined>(undefined);
  const [locality, setLocality] = useState<SelectedLocality | null>(null);

  useEffect(() => {
    const sync = () => {
      setCity(getSelectedCity());
      setLocality(getSelectedLocality());
    };
    sync();
    return subscribeToLocationChanges(sync);
  }, []);

  return { city, locality };
}
