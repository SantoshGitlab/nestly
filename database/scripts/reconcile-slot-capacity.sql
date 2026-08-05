-- Rebuilds slot_booking_counter from the bookings that actually occupy each
-- slot.
--
-- Why this exists: until the capacity release path was added, booked_count was
-- only ever incremented - at booking creation, before payment - and never
-- decremented on cancellation, refund or reschedule. Any environment that ran
-- without the release path has counters that drift permanently above reality,
-- so its slot windows start refusing bookings while standing half empty. This
-- script repairs that drift once; the application keeps the counters correct
-- from then on.
--
-- Occupying statuses are every state in which a booking still holds its seat:
-- awaiting payment, paid, or in fulfilment. Cancelled, refunded and completed
-- bookings do not (a completed booking's slot is in the past, and counters are
-- only ever read for dates being booked).
--
-- Safe to re-run. Run it during a quiet window: it takes a brief lock on the
-- counter rows, and a booking created mid-run could be counted by the rebuild
-- and then incremented again by the application. Verify first with the SELECT
-- at the bottom.
--
-- Usage: psql "$DATABASE_URL" -f database/scripts/reconcile-slot-capacity.sql

BEGIN;

WITH occupied AS (
    SELECT b.slot_window_id, b.slot_date, COUNT(*)::int AS live_count
    FROM booking b
    JOIN slot_window w ON w.id = b.slot_window_id
    WHERE w.max_bookings_per_slot IS NOT NULL
      AND b.status IN (
          'Initiated', 'PaymentPending', 'PaymentFailed', 'Confirmed',
          'AwaitingFulfilment', 'Assigned', 'InProgress', 'Rescheduled'
      )
    GROUP BY b.slot_window_id, b.slot_date
)
UPDATE slot_booking_counter c
SET booked_count = COALESCE(o.live_count, 0)
FROM slot_booking_counter c2
LEFT JOIN occupied o ON o.slot_window_id = c2.slot_window_id AND o.slot_date = c2.slot_date
WHERE c.id = c2.id
  AND c.booked_count IS DISTINCT FROM COALESCE(o.live_count, 0);

-- Counters for windows that lost their cap entirely are meaningless; drop them
-- so a cap re-applied later starts from the rebuilt truth rather than a stale
-- number.
DELETE FROM slot_booking_counter c
USING slot_window w
WHERE w.id = c.slot_window_id
  AND w.max_bookings_per_slot IS NULL;

COMMIT;

-- Verification: every row should now report drift = 0.
--
-- SELECT c.slot_window_id, c.slot_date, c.booked_count,
--        COUNT(b.id) AS live_bookings,
--        c.booked_count - COUNT(b.id) AS drift
-- FROM slot_booking_counter c
-- LEFT JOIN booking b
--        ON b.slot_window_id = c.slot_window_id
--       AND b.slot_date = c.slot_date
--       AND b.status IN ('Initiated', 'PaymentPending', 'PaymentFailed', 'Confirmed',
--                        'AwaitingFulfilment', 'Assigned', 'InProgress', 'Rescheduled')
-- GROUP BY c.slot_window_id, c.slot_date, c.booked_count
-- ORDER BY drift DESC;
