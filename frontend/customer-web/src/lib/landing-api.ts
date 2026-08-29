/** Typed client for the public curated-home-page endpoint (`LandingController`). No auth - same as `/categories`. */
import { API_V1, apiFetch } from "./api";
import type { HomeLanding } from "./landing-types";

export const getHomeLanding = () => apiFetch<HomeLanding>(`${API_V1}/landing/home`);
