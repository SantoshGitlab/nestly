import Link from "next/link";
import { Button, Card } from "@/components/ui";

/**
 * Entry point into Refer & Earn (PROVIDER-REFERRAL.md) from Profile, mirroring
 * how a referral program typically lives inside account settings rather than
 * primary navigation - this is an occasional action, not a daily one, so it
 * does not compete for a slot in ProviderSidebar/ProviderTabBar's four-item
 * nav (tuned for "providers work from a phone in the field").
 */
export function ReferralPromoSection() {
  return (
    <Card
      title="Refer & Earn"
      description="Invite another provider to Glavyx and earn once they complete their first few jobs."
    >
      <Link href="/refer-earn">
        <Button type="button" variant="secondary">
          View your referral code
        </Button>
      </Link>
    </Card>
  );
}
