/**
 * Builds the geography/catalog/serviceability/slot test data the E2E specs
 * (tasks 140a-140d) drive through the real customer-web UI against. Runs
 * entirely through admin-api HTTP calls (task 143's convention: exercise
 * the real APIs rather than inserting rows directly) - the two direct-DB
 * seeds in database/seed/ exist only for the two things no API can do
 * (bootstrapping the first admin account, and skipping unretrievable OTP
 * codes for the test customer).
 *
 * Idempotent by construction: every step looks the entity up first (rather
 * than attempting a create and falling back to a 409) and only creates when
 * genuinely missing. Relying on a 409 isn't safe here - several of these
 * tables (slot_window, the serviceability mapping tables) have no unique
 * constraint on the fields this script keys off, so a create-first approach
 * silently accumulates duplicates on every re-run instead of failing loudly
 * or reusing the existing row.
 */
const ADMIN_API = process.env.ADMIN_API_URL ?? "http://localhost:5177";
const CONSUMER_API = process.env.CONSUMER_API_URL ?? "http://localhost:5257";

export interface CatalogFixture {
  cityId: string;
  cityName: string;
  localityId: string;
  localityName: string;
  pincodeId: string;
  pincodeCode: string;
  categoryId: string;
  categorySlug: string;
  categoryName: string;
  serviceId: string;
  serviceSlug: string;
  serviceName: string;
  slotWindowId: string;
  addressId: string;
  customerAccessToken: string;
}

async function adminLogin(): Promise<string> {
  const res = await fetch(`${ADMIN_API}/api/v1/admin/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "dev-admin@nestly.local", password: "E2eTest!Passw0rd" }),
  });
  if (!res.ok) throw new Error(`Admin login failed: ${res.status} ${await res.text()}`);
  const body = await res.json();
  return body.accessToken;
}

async function customerLogin(): Promise<string> {
  const res = await fetch(`${CONSUMER_API}/api/v1/auth/login/password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: "e2e-customer@nestly.local", password: "E2eCustomer!Passw0rd" }),
  });
  if (!res.ok) throw new Error(`Customer login failed: ${res.status} ${await res.text()}`);
  const body = await res.json();
  return body.accessToken;
}

async function get(url: string, token: string): Promise<any> {
  const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`GET ${url} failed: ${res.status} ${await res.text()}`);
  return res.json();
}

async function post(url: string, token: string, body: unknown): Promise<any> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`POST ${url} failed: ${res.status} ${await res.text()}`);
  return res.status === 204 ? null : res.json();
}

/** Finds an entity in a GET-list response, creating it via POST only when genuinely absent. */
async function findOrCreate(
  token: string,
  listUrl: string,
  createUrl: string,
  createBody: unknown,
  predicate: (item: any) => boolean
): Promise<any> {
  const existing = (await get(listUrl, token)).find(predicate);
  if (existing) return existing;
  return post(createUrl, token, createBody);
}

const RUN_ID = "e2e";
const CATEGORY_SLUG = `${RUN_ID}-home-cleaning`;
const SERVICE_SLUG = `${RUN_ID}-deep-clean`;

export async function seedCatalog(): Promise<CatalogFixture> {
  const adminToken = await adminLogin();
  const A = `${ADMIN_API}/api/v1`;

  const state = await findOrCreate(
    adminToken, `${A}/admin/geography/states`, `${A}/admin/geography/states`,
    { name: "E2E State", code: "E2S" }, (s) => s.code === "E2S"
  );

  const city = await findOrCreate(
    adminToken, `${A}/admin/geography/cities`, `${A}/admin/geography/cities`,
    { stateId: state.id, name: "E2E City" }, (c) => c.name === "E2E City" && c.stateId === state.id
  );

  const zone = await findOrCreate(
    adminToken, `${A}/admin/geography/zones`, `${A}/admin/geography/zones`,
    { cityId: city.id, name: "E2E Zone" }, (z) => z.name === "E2E Zone" && z.cityId === city.id
  );

  const pincodeCode = "560001";
  const pincode = await findOrCreate(
    adminToken, `${A}/admin/geography/pincodes`, `${A}/admin/geography/pincodes`,
    { cityId: city.id, code: pincodeCode }, (pc) => pc.code === pincodeCode && pc.cityId === city.id
  );

  const locality = await findOrCreate(
    adminToken, `${A}/admin/geography/localities`, `${A}/admin/geography/localities`,
    { zoneId: zone.id, pincodeId: pincode.id, name: "E2E Locality" },
    (l) => l.name === "E2E Locality" && l.zoneId === zone.id
  );

  const category = await findOrCreate(
    adminToken, `${A}/admin/catalog/categories`, `${A}/admin/catalog/categories`,
    {
      name: "E2E Home Cleaning", slug: CATEGORY_SLUG, description: "Seeded for E2E tests.",
      iconUrl: null, bannerUrl: null, sortOrder: 0, seoTitle: null, seoMetaDescription: null,
    },
    (c) => c.slug === CATEGORY_SLUG
  );
  await post(`${A}/admin/catalog/categories/${category.id}/activate`, adminToken, null);

  const service = await findOrCreate(
    adminToken, `${A}/admin/catalog/services?categoryId=${category.id}`, `${A}/admin/catalog/services`,
    {
      categoryId: category.id, name: "E2E Deep Cleaning", slug: SERVICE_SLUG,
      description: "Seeded for E2E tests.", shortDescription: "Deep cleaning", price: 999,
      inclusions: "Full home deep clean", exclusions: "Exterior windows",
      cancellationPolicy: "Free cancellation up to 2 hours before the slot.",
      reschedulePolicy: "Free reschedule up to 2 hours before the slot.",
      durationMinutes: 120, sortOrder: 0, seoTitle: null, seoMetaDescription: null,
      pricingType: "Fixed", isTaxApplicable: true, isAddOnAllowed: false, isQuantityAllowed: false,
      isInspectionBased: false, isSlotRequired: true, isAddressRequired: true, isCustomerNoteAllowed: true,
    },
    (s) => s.slug === SERVICE_SLUG
  );
  await post(`${A}/admin/catalog/services/${service.id}/activate`, adminToken, null);

  // No unique constraint on (categoryId, cityId) / (serviceId, pincodeId) -
  // must check before creating or every re-run adds a duplicate mapping.
  const categoryCityMappings = await get(`${A}/admin/serviceability-mappings/category-city?categoryId=${category.id}`, adminToken);
  if (!categoryCityMappings.some((m: any) => m.cityId === city.id)) {
    await post(`${A}/admin/serviceability-mappings/category-city`, adminToken, { categoryId: category.id, cityId: city.id });
  }

  const servicePincodeMappings = await get(`${A}/admin/serviceability-mappings/service-pincode?serviceId=${service.id}`, adminToken);
  if (!servicePincodeMappings.some((m: any) => m.pincodeId === pincode.id)) {
    await post(`${A}/admin/serviceability-mappings/service-pincode`, adminToken, { serviceId: service.id, pincodeId: pincode.id });
  }

  // Covers the full day (rather than a narrow window) so the E2E suite's
  // slot selection is bookable regardless of what time of day the suite
  // happens to run - SlotAvailabilityService filters on window.StartTime >=
  // now + cutoff, so a narrow window can go stale mid-run.
  const slotWindow = await findOrCreate(
    adminToken, `${A}/admin/slots/windows?cityId=${city.id}`, `${A}/admin/slots/windows`,
    {
      cityId: city.id, name: "E2E Anytime", startTime: "00:00:00", endTime: "23:59:00",
      maxBookingsPerSlot: 50, daysOfWeek: [0, 1, 2, 3, 4, 5, 6],
    },
    (w) => w.name === "E2E Anytime"
  );
  await post(`${A}/admin/slots/windows/${slotWindow.id}/activate`, adminToken, null);

  await fetch(`${A}/admin/slots/booking-policies`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({ cityId: city.id, cutoffMinutes: 0, maxAdvanceDays: 30 }),
  });

  const customerToken = await customerLogin();
  const addresses = await get(`${CONSUMER_API}/api/v1/addresses`, customerToken);
  let address = addresses.find((a: any) => a.pincode === pincodeCode);
  if (!address) {
    address = await post(`${CONSUMER_API}/api/v1/addresses`, customerToken, {
      label: "E2E Home",
      line1: "123 E2E Street",
      line2: null,
      landmark: null,
      pincode: pincodeCode,
      city: city.name,
      state: state.name,
      latitude: 12.9716,
      longitude: 77.5946,
      contactName: "E2E Test Customer",
      contactMobile: "+919999999999",
      isDefault: true,
    });
  }

  return {
    cityId: city.id,
    cityName: city.name,
    localityId: locality.id,
    localityName: locality.name,
    pincodeId: pincode.id,
    pincodeCode,
    categoryId: category.id,
    categorySlug: CATEGORY_SLUG,
    categoryName: "E2E Home Cleaning",
    serviceId: service.id,
    serviceSlug: SERVICE_SLUG,
    serviceName: "E2E Deep Cleaning",
    slotWindowId: slotWindow.id,
    addressId: address.id,
    customerAccessToken: customerToken,
  };
}
