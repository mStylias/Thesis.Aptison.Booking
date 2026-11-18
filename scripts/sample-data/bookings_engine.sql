-- Weighted pick over a cumulative weight array (ascending). Returns a 1-based index.
CREATE OR REPLACE FUNCTION pg_temp.seed_pick(cumulative double precision[]) RETURNS int
    LANGUAGE plpgsql VOLATILE AS
$$
DECLARE
    target double precision := random() * cumulative[array_length(cumulative, 1)];
    lo     int := 1;
    hi     int := array_length(cumulative, 1);
    mid    int;
BEGIN
    WHILE lo < hi LOOP
        mid := (lo + hi) / 2;
        IF cumulative[mid] < target THEN
            lo := mid + 1;
        ELSE
            hi := mid;
        END IF;
    END LOOP;
    RETURN lo;
END
$$;

CREATE OR REPLACE FUNCTION pg_temp.seed_text(p_kind text, p_rating int DEFAULT NULL, p_lang text DEFAULT 'el') RETURNS text
    LANGUAGE sql VOLATILE AS
$$
SELECT body FROM seed_text
WHERE kind = p_kind AND (p_rating IS NULL OR rating = p_rating) AND lang = p_lang
ORDER BY random() LIMIT 1
$$;

-- Season, weekday and hour weights for the demand curve.
CREATE OR REPLACE FUNCTION pg_temp.seed_demand(p_day date, p_minute int) RETURNS double precision
    LANGUAGE sql IMMUTABLE AS
$$
SELECT (ARRAY[0.0, 0.62, 0.72, 0.78, 0.88, 0.97, 1.0])[extract(dow FROM p_day)::int + 1]
     * (ARRAY[0.82, 0.85, 0.95, 1.05, 1.0, 1.06, 0.95, 0.66, 1.0, 1.0, 0.95, 1.12])[extract(month FROM p_day)::int]
     * CASE WHEN extract(month FROM p_day) = 12 AND extract(day FROM p_day) BETWEEN 15 AND 24 THEN 1.22 ELSE 1.0 END
     * (ARRAY[0.5, 0.72, 0.9, 0.95, 0.78, 0.6, 0.62, 0.78, 0.95, 1.0, 0.95, 0.72, 0.5])[least(greatest(p_minute / 60 - 8, 1), 13)]
$$;

DO
$$
DECLARE
    v_now         constant timestamptz := now();
    v_today       constant date := CURRENT_DATE;
    v_shop        record;
    v_tz          text;
    v_currency    text;
    v_day         date;
    v_offset      int;
    v_factor      double precision;
    v_closed      boolean;
    v_override    record;
    v_staff       record;
    v_window      record;
    v_cursor      int;
    v_end         int;
    v_p           double precision;

    -- customer pool of the shop, as parallel arrays
    v_pool_ids    uuid[];
    v_pool_walk   boolean[];
    v_pool_cum    double precision[];
    v_pool_start  int[];
    v_pool_end    int[];
    v_pool_noshow boolean[];
    v_pool_lang   text[];
    v_pool_future int[];
    v_pool_next   int[];
    v_pool_size   int;
    v_used_today  int[];
    v_pick        int;
    v_try         int;

    -- offers of the barber
    v_main_ids    uuid[];
    v_main_price  numeric[];
    v_main_dur    int[];
    v_main_bb     int[];
    v_main_ba     int[];
    v_main_cum    double precision[];
    v_add_ids     uuid[];
    v_add_price   numeric[];
    v_add_dur     int[];
    v_add_ba      int[];
    v_m           int;
    v_a           int;

    v_minutes     int;
    v_block_after int;
    v_start_local timestamp;
    v_starts_at   timestamptz;
    v_ends_at     timestamptz;
    v_blocked     timestamptz;
    v_clash       timestamptz;
    v_customer    uuid;
    v_walk_in     boolean;
    v_lang        text;
    v_source      text;
    v_status      text;
    v_lead        double precision;
    v_created_at  timestamptz;
    v_created_by  uuid;
    v_cancel_at   timestamptz;
    v_cancel_by   uuid;
    v_reason      text;
    v_notes       text;
    v_internal    text;
    v_id          uuid;
    v_r           double precision;
    v_rating      int;
    v_review_at   timestamptz;
    v_total       numeric;
BEGIN
    PERFORM setseed(0.2026);

    FOR v_shop IN SELECT * FROM seed_shop_plan ORDER BY sort_key LOOP
        SELECT timezone, currency INTO v_tz, v_currency FROM companies WHERE id = v_shop.company_id;

        SELECT array_agg(user_id ORDER BY ord), array_agg(is_walk_in ORDER BY ord),
               array_agg(sum_weight ORDER BY ord), array_agg(start_offset ORDER BY ord),
               array_agg(coalesce(end_offset, 100000) ORDER BY ord), array_agg(noshow_prone ORDER BY ord),
               array_agg(lang ORDER BY ord)
        INTO v_pool_ids, v_pool_walk, v_pool_cum, v_pool_start, v_pool_end, v_pool_noshow, v_pool_lang
        FROM (SELECT pool.*, row_number() OVER (ORDER BY pool.user_id) AS ord,
                     sum(pool.weight) OVER (ORDER BY pool.user_id) AS sum_weight
              FROM seed_pool AS pool WHERE pool.company_id = v_shop.company_id) AS ordered;

        v_pool_size := coalesce(array_length(v_pool_ids, 1), 0);
        IF v_pool_size = 0 THEN
            CONTINUE;
        END IF;
        v_pool_future := array_fill(0, ARRAY[v_pool_size]);
        v_pool_next := v_pool_start;

        FOR v_day IN SELECT generate_series(v_today + v_shop.from_offset, v_today + v_shop.to_offset, interval '1 day')::date LOOP
            v_offset := v_day - v_today;
            v_used_today := '{}';

            SELECT holiday.is_closed, holiday.override_open_time AS open_time, holiday.override_close_time AS close_time
            INTO v_override
            FROM company_holidays AS holiday
            WHERE holiday.owner_id = v_shop.company_id AND NOT holiday.is_deleted
              AND v_day BETWEEN holiday.starts_on AND holiday.ends_on
            LIMIT 1;
            IF FOUND AND (v_override.is_closed OR v_override.open_time IS NULL) THEN
                CONTINUE;
            END IF;

            IF v_offset <= 0 THEN
                v_factor := v_shop.growth_from + (1 - v_shop.growth_from)
                            * (v_offset - v_shop.from_offset)::double precision / greatest(-v_shop.from_offset, 1);
            ELSE
                v_factor := greatest(0.05, 0.95 * exp(-v_offset / v_shop.future_decay_days));
            END IF;

            FOR v_staff IN
                SELECT member.id, member.user_id
                FROM staff_members AS member
                WHERE member.owner_id = v_shop.company_id AND member.is_bookable AND NOT member.is_deleted
                  AND member.joined_at::date <= v_day
                  AND (member.is_active OR v_day < coalesce(member.updated_at, member.joined_at)::date)
                ORDER BY member.id
            LOOP
                SELECT array_agg(offer.service_id ORDER BY offer.service_id), array_agg(offer.price ORDER BY offer.service_id),
                       array_agg(offer.duration ORDER BY offer.service_id), array_agg(offer.buffer_before ORDER BY offer.service_id),
                       array_agg(offer.buffer_after ORDER BY offer.service_id), array_agg(offer.cum ORDER BY offer.service_id)
                INTO v_main_ids, v_main_price, v_main_dur, v_main_bb, v_main_ba, v_main_cum
                FROM (SELECT seed_offer.*, sum(seed_offer.weight) OVER (ORDER BY seed_offer.service_id) AS cum
                      FROM seed_offer WHERE seed_offer.staff_member_id = v_staff.id AND NOT seed_offer.is_addon) AS offer;
                IF v_main_ids IS NULL THEN
                    CONTINUE;
                END IF;
                SELECT array_agg(offer.service_id ORDER BY offer.service_id), array_agg(offer.price ORDER BY offer.service_id),
                       array_agg(offer.duration ORDER BY offer.service_id), array_agg(offer.buffer_after ORDER BY offer.service_id)
                INTO v_add_ids, v_add_price, v_add_dur, v_add_ba
                FROM seed_offer AS offer WHERE offer.staff_member_id = v_staff.id AND offer.is_addon;

                FOR v_window IN
                    SELECT greatest(work.start_time, shop.open_time) AS open_time,
                           least(work.end_time, shop.close_time) AS close_time
                    FROM staff_work_hours AS work
                    JOIN (SELECT hours.open_time, hours.close_time FROM company_business_hours AS hours
                          WHERE hours.owner_id = v_shop.company_id AND NOT hours.is_deleted
                            AND hours.day_of_week = extract(dow FROM v_day)::int AND v_override IS NULL
                          UNION ALL
                          SELECT v_override.open_time, v_override.close_time WHERE v_override IS NOT NULL) AS shop
                      ON work.start_time < shop.close_time AND work.end_time > shop.open_time
                    WHERE work.staff_member_id = v_staff.id AND NOT work.is_deleted
                      AND work.day_of_week = extract(dow FROM v_day)::int
                    ORDER BY 1
                LOOP
                    v_cursor := extract(hour FROM v_window.open_time)::int * 60 + extract(minute FROM v_window.open_time)::int;
                    v_end := extract(hour FROM v_window.close_time)::int * 60 + extract(minute FROM v_window.close_time)::int;

                    WHILE v_cursor + 15 <= v_end LOOP
                        v_p := v_shop.density * v_factor * pg_temp.seed_demand(v_day, v_cursor);
                        IF random() >= v_p THEN
                            v_cursor := v_cursor + v_shop.slot_minutes;
                            CONTINUE;
                        END IF;

                        v_m := pg_temp.seed_pick(v_main_cum);
                        v_minutes := v_main_dur[v_m];
                        v_block_after := v_main_bb[v_m] + v_main_ba[v_m];
                        v_a := NULL;
                        IF v_add_ids IS NOT NULL AND random() < 0.24 THEN
                            v_a := 1 + floor(random() * array_length(v_add_ids, 1))::int;
                            v_minutes := v_minutes + v_add_dur[v_a];
                            v_block_after := v_main_bb[v_m] + v_add_ba[v_a];
                        END IF;
                        IF v_cursor + v_minutes > v_end THEN
                            EXIT;
                        END IF;

                        v_start_local := v_day + make_interval(mins => v_cursor);
                        v_starts_at := v_start_local AT TIME ZONE v_tz;
                        v_ends_at := v_starts_at + make_interval(mins => v_minutes);
                        v_blocked := v_ends_at + make_interval(mins => v_block_after);

                        SELECT max(other.blocked_until) INTO v_clash
                        FROM appointments AS other
                        WHERE other.staff_member_id = v_staff.id AND NOT other.is_deleted
                          AND other.blocked_from < v_blocked AND other.blocked_until > v_starts_at;
                        IF v_clash IS NOT NULL THEN
                            v_cursor := (extract(epoch FROM (v_clash AT TIME ZONE v_tz) - v_day::timestamp) / 60)::int;
                            v_cursor := ((v_cursor + v_shop.slot_minutes - 1) / v_shop.slot_minutes) * v_shop.slot_minutes;
                            CONTINUE;
                        END IF;
                        IF EXISTS (SELECT 1 FROM staff_time_off AS off
                                   WHERE off.staff_member_id = v_staff.id AND NOT off.is_deleted
                                     AND off.starts_at < v_blocked AND off.ends_at > v_starts_at) THEN
                            v_cursor := v_cursor + v_shop.slot_minutes;
                            CONTINUE;
                        END IF;

                        v_pick := NULL;
                        FOR v_try IN 1..14 LOOP
                            v_pick := pg_temp.seed_pick(v_pool_cum);
                            EXIT WHEN v_pool_next[v_pick] <= v_offset AND v_offset <= v_pool_end[v_pick]
                                  AND NOT (v_pick = ANY (v_used_today))
                                  AND (v_starts_at <= v_now OR v_pool_future[v_pick] < 2)
                                  AND (v_pool_walk[v_pick] OR NOT EXISTS (
                                      SELECT 1 FROM appointments AS own
                                      WHERE own.customer_id = v_pool_ids[v_pick] AND NOT own.is_deleted
                                        AND own.starts_at < v_ends_at + interval '30 minutes'
                                        AND own.ends_at > v_starts_at - interval '30 minutes'));
                            v_pick := NULL;
                        END LOOP;
                        IF v_pick IS NULL THEN
                            v_cursor := v_cursor + v_shop.slot_minutes;
                            CONTINUE;
                        END IF;
                        v_used_today := v_used_today || v_pick;
                        v_pool_next[v_pick] := v_offset + 14 + floor(random() * 22)::int;
                        v_customer := v_pool_ids[v_pick];
                        v_walk_in := v_pool_walk[v_pick];
                        v_lang := v_pool_lang[v_pick];

                        v_source := CASE WHEN NOT v_walk_in AND random() < v_shop.online_share THEN 'online' ELSE 'staff' END;
                        v_r := random();
                        IF v_source = 'online' THEN
                            v_lead := CASE WHEN v_r < 0.14 THEN random() * 0.5
                                           WHEN v_r < 0.50 THEN 0.5 + random() * 2.5
                                           WHEN v_r < 0.82 THEN 3 + random() * 8
                                           WHEN v_r < 0.96 THEN 11 + random() * 20
                                           ELSE 31 + random() * 30 END;
                            v_created_by := v_customer;
                        ELSE
                            v_lead := CASE WHEN v_r < 0.45 THEN random() * 0.15
                                           WHEN v_r < 0.90 THEN 1 + random() * 6
                                           ELSE 7 + random() * 21 END;
                            v_created_by := v_shop.bookers[1 + floor(random() * array_length(v_shop.bookers, 1))::int];
                            IF random() < 0.5 THEN
                                v_created_by := v_staff.user_id;
                            END IF;
                        END IF;
                        v_created_at := least(v_starts_at - interval '20 minutes' - make_interval(secs => v_lead * 86400),
                                              v_now - make_interval(mins => 5 + floor(random() * 600)::int));
                        IF v_created_at > v_starts_at - interval '10 minutes' THEN
                            v_created_at := v_starts_at - interval '10 minutes';
                        END IF;

                        v_cancel_at := NULL;
                        v_cancel_by := NULL;
                        v_reason := NULL;
                        v_r := random();
                        IF v_ends_at <= v_now THEN
                            v_status := CASE
                                WHEN v_r < CASE WHEN v_pool_noshow[v_pick] THEN 0.22 ELSE 0.035 END THEN 'no_show'
                                WHEN v_r < CASE WHEN v_pool_noshow[v_pick] THEN 0.32 ELSE 0.11 END THEN 'cancelled'
                                ELSE 'completed' END;
                        ELSIF v_starts_at <= v_now THEN
                            v_status := 'confirmed';
                        ELSE
                            v_status := CASE WHEN v_r < 0.05 THEN 'cancelled' ELSE 'confirmed' END;
                        END IF;
                        IF v_status = 'cancelled' THEN
                            v_cancel_at := v_created_at + (least(v_starts_at, v_now) - v_created_at) * (0.2 + random() * 0.8);
                            IF NOT v_walk_in AND random() < 0.62 THEN
                                v_cancel_by := v_customer;
                                IF random() < 0.55 THEN v_reason := pg_temp.seed_text('cancel_customer'); END IF;
                            ELSE
                                v_cancel_by := v_shop.bookers[1 + floor(random() * array_length(v_shop.bookers, 1))::int];
                                IF random() < 0.7 THEN v_reason := pg_temp.seed_text('cancel_shop'); END IF;
                            END IF;
                        END IF;

                        v_notes := CASE WHEN v_source = 'online' AND random() < 0.08 THEN pg_temp.seed_text('booking_note') END;
                        v_internal := CASE WHEN random() < 0.05 THEN pg_temp.seed_text('internal_note') END;
                        v_total := v_main_price[v_m] + CASE WHEN v_a IS NULL THEN 0 ELSE v_add_price[v_a] END;
                        v_id := uuidv7();

                        INSERT INTO appointments (id, owner_id, staff_member_id, customer_id, starts_at, ends_at,
                                                  blocked_from, blocked_until, status, notes, internal_notes,
                                                  cancellation_reason, cancelled_at, cancelled_by_id, total_price, currency,
                                                  source, duration_overridden, calendar_sequence,
                                                  created_at, created_by_id, updated_at, modified_by_id)
                        VALUES (v_id, v_shop.company_id, v_staff.id, v_customer, v_starts_at, v_ends_at,
                                v_starts_at, v_blocked, v_status, v_notes, v_internal,
                                v_reason, v_cancel_at, v_cancel_by, v_total, v_currency,
                                v_source, false, CASE WHEN v_status = 'cancelled' THEN 1 ELSE 0 END,
                                v_created_at, v_created_by,
                                CASE WHEN v_status IN ('completed', 'no_show') THEN v_ends_at + make_interval(mins => 2 + floor(random() * 40)::int)
                                     WHEN v_status = 'cancelled' THEN v_cancel_at END,
                                CASE WHEN v_status IN ('completed', 'no_show') THEN v_staff.user_id
                                     WHEN v_status = 'cancelled' THEN v_cancel_by END);

                        INSERT INTO appointment_services (id, owner_id, appointment_id, service_id, price_at_booking,
                                                          duration_at_booking_minutes, created_at, created_by_id)
                        VALUES (uuidv7(), v_shop.company_id, v_id, v_main_ids[v_m], v_main_price[v_m], v_main_dur[v_m],
                                v_created_at, v_created_by);
                        IF v_a IS NOT NULL THEN
                            INSERT INTO appointment_services (id, owner_id, appointment_id, service_id, price_at_booking,
                                                              duration_at_booking_minutes, created_at, created_by_id)
                            VALUES (uuidv7(), v_shop.company_id, v_id, v_add_ids[v_a], v_add_price[v_a], v_add_dur[v_a],
                                    v_created_at, v_created_by);
                        END IF;

                        IF v_starts_at > v_now AND v_status <> 'cancelled' THEN
                            v_pool_future[v_pick] := v_pool_future[v_pick] + 1;
                        END IF;

                        IF v_status = 'completed' AND NOT v_walk_in AND random() < v_shop.review_rate THEN
                            v_review_at := v_ends_at + make_interval(mins => 120 + floor(random() * 4200)::int);
                            IF v_review_at < v_now - interval '1 hour' THEN
                                v_r := (SELECT coalesce(max(bias), 0) FROM seed_staff_bias WHERE staff_member_id = v_staff.id);
                                v_r := v_r + random() * (1 - v_r);
                                v_rating := CASE WHEN v_r < 0.62 THEN 5 WHEN v_r < 0.87 THEN 4 WHEN v_r < 0.95 THEN 3
                                                 WHEN v_r < 0.985 THEN 2 ELSE 1 END;
                                INSERT INTO reviews (id, owner_id, appointment_id, customer_id, staff_member_id, rating,
                                                     comment, is_published, created_at, created_by_id)
                                VALUES (uuidv7(), v_shop.company_id, v_id, v_customer, v_staff.id, v_rating,
                                        CASE WHEN random() < 0.45 THEN
                                            coalesce(pg_temp.seed_text('review', v_rating, v_lang), pg_temp.seed_text('review', v_rating, 'el'))
                                        END,
                                        random() < 0.96, v_review_at, v_customer);
                            END IF;
                        END IF;

                        v_cursor := v_cursor + v_minutes + v_block_after;
                        v_cursor := ((v_cursor + v_shop.slot_minutes - 1) / v_shop.slot_minutes) * v_shop.slot_minutes;
                    END LOOP;
                END LOOP;
            END LOOP;
        END LOOP;
    END LOOP;
END
$$;
