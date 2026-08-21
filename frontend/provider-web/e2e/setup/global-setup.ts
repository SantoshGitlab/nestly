import fs from "node:fs";
import path from "node:path";
import { seedProviderFixture } from "./seed-provider-job";

const FIXTURE_PATH = path.join(__dirname, "fixture.json");

export default async function globalSetup() {
  const fixture = await seedProviderFixture();
  fs.writeFileSync(FIXTURE_PATH, JSON.stringify(fixture, null, 2));
}
