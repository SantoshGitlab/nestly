--
-- PostgreSQL database dump
--

\restrict aU35IRHRsbnfzsjmlTkpO3DQkYSnXktPtPzO2K4TTxCjypYIwYLdO9ncGR5MJsP

-- Dumped from database version 16.14
-- Dumped by pg_dump version 16.14

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: hangfire; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA hangfire;


--
-- Name: btree_gist; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;


--
-- Name: EXTENSION btree_gist; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION btree_gist IS 'support for indexing common datatypes in GiST';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: aggregatedcounter; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.aggregatedcounter (
    id bigint NOT NULL,
    key text NOT NULL,
    value bigint NOT NULL,
    expireat timestamp with time zone
);


--
-- Name: aggregatedcounter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.aggregatedcounter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aggregatedcounter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.aggregatedcounter_id_seq OWNED BY hangfire.aggregatedcounter.id;


--
-- Name: counter; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.counter (
    id bigint NOT NULL,
    key text NOT NULL,
    value bigint NOT NULL,
    expireat timestamp with time zone
);


--
-- Name: counter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.counter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: counter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.counter_id_seq OWNED BY hangfire.counter.id;


--
-- Name: hash; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.hash (
    id bigint NOT NULL,
    key text NOT NULL,
    field text NOT NULL,
    value text,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: hash_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.hash_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: hash_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.hash_id_seq OWNED BY hangfire.hash.id;


--
-- Name: job; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.job (
    id bigint NOT NULL,
    stateid bigint,
    statename text,
    invocationdata jsonb NOT NULL,
    arguments jsonb NOT NULL,
    createdat timestamp with time zone NOT NULL,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: job_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.job_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: job_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.job_id_seq OWNED BY hangfire.job.id;


--
-- Name: jobparameter; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.jobparameter (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    name text NOT NULL,
    value text,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: jobparameter_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.jobparameter_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: jobparameter_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.jobparameter_id_seq OWNED BY hangfire.jobparameter.id;


--
-- Name: jobqueue; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.jobqueue (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    queue text NOT NULL,
    fetchedat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: jobqueue_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.jobqueue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: jobqueue_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.jobqueue_id_seq OWNED BY hangfire.jobqueue.id;


--
-- Name: list; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.list (
    id bigint NOT NULL,
    key text NOT NULL,
    value text,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: list_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.list_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: list_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.list_id_seq OWNED BY hangfire.list.id;


--
-- Name: lock; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.lock (
    resource text NOT NULL,
    updatecount integer DEFAULT 0 NOT NULL,
    acquired timestamp with time zone
);


--
-- Name: schema; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.schema (
    version integer NOT NULL
);


--
-- Name: server; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.server (
    id text NOT NULL,
    data jsonb,
    lastheartbeat timestamp with time zone NOT NULL,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: set; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.set (
    id bigint NOT NULL,
    key text NOT NULL,
    score double precision NOT NULL,
    value text NOT NULL,
    expireat timestamp with time zone,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: set_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.set_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: set_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.set_id_seq OWNED BY hangfire.set.id;


--
-- Name: state; Type: TABLE; Schema: hangfire; Owner: -
--

CREATE TABLE hangfire.state (
    id bigint NOT NULL,
    jobid bigint NOT NULL,
    name text NOT NULL,
    reason text,
    createdat timestamp with time zone NOT NULL,
    data jsonb,
    updatecount integer DEFAULT 0 NOT NULL
);


--
-- Name: state_id_seq; Type: SEQUENCE; Schema: hangfire; Owner: -
--

CREATE SEQUENCE hangfire.state_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: state_id_seq; Type: SEQUENCE OWNED BY; Schema: hangfire; Owner: -
--

ALTER SEQUENCE hangfire.state_id_seq OWNED BY hangfire.state.id;


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL
);


--
-- Name: admin_permission; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.admin_permission (
    id uuid NOT NULL,
    code character varying(100) NOT NULL,
    module character varying(100) NOT NULL,
    description character varying(1000) NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: admin_role; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.admin_role (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(1000) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: admin_user; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.admin_user (
    id uuid NOT NULL,
    email character varying(200) NOT NULL,
    password_hash character varying(500) NOT NULL,
    full_name character varying(200) NOT NULL,
    status character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    failed_login_attempts integer DEFAULT 0 NOT NULL,
    locked_until_utc timestamp with time zone,
    role_id uuid
);


--
-- Name: amc_plan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.amc_plan (
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    name character varying(150) NOT NULL,
    description character varying(500),
    price numeric(12,2) NOT NULL,
    term_months integer NOT NULL,
    visits_included integer NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: amc_service_visit; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.amc_service_visit (
    id uuid NOT NULL,
    contract_id uuid NOT NULL,
    booking_id uuid NOT NULL,
    consumed_at_utc timestamp with time zone NOT NULL
);


--
-- Name: audit_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.audit_log (
    id uuid NOT NULL,
    actor_type character varying(20) NOT NULL,
    actor_id uuid,
    entity_name character varying(100) NOT NULL,
    entity_id character varying(100) NOT NULL,
    action character varying(100) NOT NULL,
    old_values jsonb,
    new_values jsonb,
    ip_address character varying(64),
    correlation_id character varying(100),
    occurred_on_utc timestamp with time zone NOT NULL
);


--
-- Name: banner; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.banner (
    id uuid NOT NULL,
    title character varying(200) NOT NULL,
    media_id uuid NOT NULL,
    link_url character varying(2000),
    placement character varying(20) NOT NULL,
    category_id uuid,
    sort_order integer NOT NULL,
    status character varying(20) NOT NULL,
    publish_start_utc timestamp with time zone,
    publish_end_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    subtitle character varying(300)
);


--
-- Name: booking; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    customer_name_snapshot character varying(200) NOT NULL,
    customer_mobile_snapshot character varying(20) NOT NULL,
    source_address_id uuid,
    address_label_snapshot character varying(100) NOT NULL,
    address_line1snapshot character varying(300) NOT NULL,
    address_line2snapshot character varying(300),
    address_landmark_snapshot character varying(200),
    address_pincode_snapshot character varying(10) NOT NULL,
    address_city_snapshot character varying(100) NOT NULL,
    address_state_snapshot character varying(100) NOT NULL,
    address_latitude_snapshot numeric(9,6) NOT NULL,
    address_longitude_snapshot numeric(9,6) NOT NULL,
    address_contact_name_snapshot character varying(200) NOT NULL,
    address_contact_mobile_snapshot character varying(20) NOT NULL,
    slot_window_id uuid NOT NULL,
    slot_date date NOT NULL,
    slot_window_name_snapshot character varying(100) NOT NULL,
    slot_start_time_snapshot interval NOT NULL,
    slot_end_time_snapshot interval NOT NULL,
    base_price_snapshot numeric(12,2) NOT NULL,
    quantity_snapshot integer NOT NULL,
    base_total_snapshot numeric(12,2) NOT NULL,
    add_on_total_snapshot numeric(12,2) NOT NULL,
    visit_charge_snapshot numeric(12,2) NOT NULL,
    subtotal_snapshot numeric(12,2) NOT NULL,
    tax_percentage_snapshot numeric(5,2) NOT NULL,
    tax_amount_snapshot numeric(12,2) NOT NULL,
    platform_fee_snapshot numeric(12,2) NOT NULL,
    total_payable_snapshot numeric(12,2) NOT NULL,
    coupon_code_snapshot character varying(50),
    coupon_discount_amount_snapshot numeric(12,2),
    status character varying(30) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    assigned_provider_id uuid,
    subscription_discount_amount_snapshot numeric(12,2),
    subscription_free_visit_applied boolean DEFAULT false NOT NULL,
    subscription_id uuid,
    idempotency_key character varying(100),
    recurring_booking_plan_id uuid,
    wallet_credit_applied_snapshot numeric(12,2),
    amc_contract_id uuid,
    is_duration_based_snapshot boolean DEFAULT false NOT NULL,
    service_duration_minutes_snapshot integer DEFAULT 0 NOT NULL,
    booking_reference character varying(20) DEFAULT ''::character varying NOT NULL
);


--
-- Name: booking_addon_item; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_addon_item (
    id uuid NOT NULL,
    booking_item_id uuid NOT NULL,
    service_add_on_id uuid NOT NULL,
    name_snapshot character varying(200) NOT NULL,
    unit_price_snapshot numeric(12,2) NOT NULL,
    quantity integer NOT NULL,
    line_total_snapshot numeric(12,2) NOT NULL,
    add_on_group_id uuid,
    group_name_snapshot character varying(200)
);


--
-- Name: booking_cancellation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_cancellation (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    actor character varying(20) NOT NULL,
    reason character varying(500) NOT NULL,
    within_free_cancellation_window boolean NOT NULL,
    cancellation_fee_amount numeric(12,2) NOT NULL,
    refund_amount numeric(12,2) NOT NULL,
    refund_method character varying(20),
    refund_transaction_id uuid,
    internal_notes character varying(1000),
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: booking_completion_proof; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_completion_proof (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    submitted_by_provider_id uuid NOT NULL,
    submitted_at_utc timestamp with time zone NOT NULL,
    photo_refs_json jsonb NOT NULL,
    checklist_answers_json jsonb NOT NULL
);


--
-- Name: booking_item; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_item (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    service_id uuid NOT NULL,
    name_snapshot character varying(200) NOT NULL,
    slug_snapshot character varying(200) NOT NULL,
    unit_price_snapshot numeric(12,2) NOT NULL,
    quantity integer NOT NULL,
    line_total_snapshot numeric(12,2) NOT NULL,
    service_variant_id uuid,
    variant_duration_minutes_snapshot integer,
    variant_name_snapshot character varying(200),
    service_group_id uuid,
    service_group_name_snapshot character varying(200)
);


--
-- Name: booking_provider_assignment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_provider_assignment (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    provider_id uuid NOT NULL,
    assigned_by_type character varying(20) NOT NULL,
    assigned_by_user_id uuid,
    assigned_at timestamp with time zone NOT NULL,
    status character varying(20) NOT NULL,
    response_deadline timestamp with time zone,
    responded_at timestamp with time zone,
    notes character varying(500),
    completion_proof_ref character varying(500),
    completed_at timestamp with time zone
);


--
-- Name: booking_reschedule; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_reschedule (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    actor character varying(20) NOT NULL,
    reason character varying(500),
    from_slot_window_id uuid NOT NULL,
    from_slot_date date NOT NULL,
    from_slot_start_time interval NOT NULL,
    from_slot_end_time interval NOT NULL,
    to_slot_window_id uuid NOT NULL,
    to_slot_date date NOT NULL,
    to_slot_start_time interval NOT NULL,
    to_slot_end_time interval NOT NULL,
    is_late boolean NOT NULL,
    fee_amount numeric(12,2) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: booking_status_history; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_status_history (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    from_status character varying(30),
    to_status character varying(30) NOT NULL,
    reason character varying(500),
    changed_at_utc timestamp with time zone NOT NULL
);


--
-- Name: booking_tracking; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.booking_tracking (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    provider_id uuid,
    eta_seconds integer,
    eta_distance_metres integer,
    eta_computed_at_utc timestamp with time zone,
    eta_source character varying(20),
    eta_origin_latitude numeric(9,6),
    eta_origin_longitude numeric(9,6)
);


--
-- Name: category; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.category (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    slug character varying(200) NOT NULL,
    description character varying(2000) NOT NULL,
    icon_url character varying(500),
    banner_url character varying(500),
    is_active boolean NOT NULL,
    is_featured boolean NOT NULL,
    sort_order integer NOT NULL,
    seo_title character varying(200),
    seo_meta_description character varying(500),
    parent_category_id uuid,
    page_banner_url character varying(500)
);


--
-- Name: category_city_mapping; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.category_city_mapping (
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    city_id uuid NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: chat_message; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.chat_message (
    id uuid NOT NULL,
    thread_id uuid NOT NULL,
    context_type character varying(20) NOT NULL,
    context_id uuid NOT NULL,
    sender_id uuid NOT NULL,
    sender_type character varying(20) NOT NULL,
    body character varying(4000) NOT NULL,
    sent_at_utc timestamp with time zone NOT NULL,
    read_at_utc timestamp with time zone
);


--
-- Name: chat_thread; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.chat_thread (
    id uuid NOT NULL,
    context_type character varying(20) NOT NULL,
    context_id uuid NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    last_message_at_utc timestamp with time zone NOT NULL
);


--
-- Name: city; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.city (
    id uuid NOT NULL,
    state_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: city_pricing_policy; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.city_pricing_policy (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    visit_charge numeric(18,2) NOT NULL,
    tax_percentage numeric(5,2) NOT NULL,
    platform_fee numeric(18,2) NOT NULL
);


--
-- Name: cms_faq; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cms_faq (
    id uuid NOT NULL,
    question character varying(500) NOT NULL,
    answer text NOT NULL,
    placement character varying(20) NOT NULL,
    sort_order integer NOT NULL,
    status character varying(20) NOT NULL,
    publish_start_utc timestamp with time zone,
    publish_end_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);


--
-- Name: cms_media; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cms_media (
    id uuid NOT NULL,
    url character varying(2000) NOT NULL,
    alt_text character varying(300),
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: cms_page; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cms_page (
    id uuid NOT NULL,
    title character varying(200) NOT NULL,
    slug character varying(200) NOT NULL,
    body text NOT NULL,
    seo_title character varying(200),
    seo_description character varying(500),
    seo_keywords character varying(300),
    placement character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    publish_start_utc timestamp with time zone,
    publish_end_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);


--
-- Name: coupon; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.coupon (
    id uuid NOT NULL,
    code character varying(50) NOT NULL,
    description character varying(300),
    discount_type character varying(20) NOT NULL,
    discount_value numeric(12,2) NOT NULL,
    max_discount_amount numeric(12,2),
    min_order_amount numeric(12,2) NOT NULL,
    valid_from_utc timestamp with time zone NOT NULL,
    valid_to_utc timestamp with time zone NOT NULL,
    is_active boolean NOT NULL,
    usage_limit_total integer,
    usage_limit_per_customer integer,
    redemption_count integer NOT NULL,
    applicable_category_id uuid,
    customer_segment character varying(20) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    restricted_to_customer_id uuid
);


--
-- Name: coupon_customer_redemption_counter; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.coupon_customer_redemption_counter (
    id uuid NOT NULL,
    coupon_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    reserved_count integer NOT NULL
);


--
-- Name: coupon_redemption; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.coupon_redemption (
    id uuid NOT NULL,
    coupon_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    booking_id uuid NOT NULL,
    discount_amount numeric(12,2) NOT NULL,
    redeemed_at_utc timestamp with time zone NOT NULL
);


--
-- Name: customer; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer (
    id uuid NOT NULL,
    mobile character varying(20) NOT NULL,
    email character varying(200),
    name character varying(200) NOT NULL,
    date_of_birth timestamp with time zone,
    address character varying(200),
    city character varying(200),
    state character varying(200),
    pincode character varying(20),
    country character varying(200),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    status character varying(20) NOT NULL,
    referral_code character varying(20)
);


--
-- Name: customer_address; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_address (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    label character varying(100) NOT NULL,
    line1 character varying(300) NOT NULL,
    line2 character varying(300),
    landmark character varying(200),
    pincode character varying(12) NOT NULL,
    city character varying(100) NOT NULL,
    state character varying(100) NOT NULL,
    latitude numeric(9,6) NOT NULL,
    longitude numeric(9,6) NOT NULL,
    contact_name character varying(200) NOT NULL,
    contact_mobile character varying(20) NOT NULL,
    is_default boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    locality_id uuid,
    pincode_id uuid
);


--
-- Name: customer_amc_contract; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_amc_contract (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    plan_id uuid NOT NULL,
    plan_name_snapshot character varying(150) NOT NULL,
    category_id_snapshot uuid NOT NULL,
    price_snapshot numeric(12,2) NOT NULL,
    term_months_snapshot integer NOT NULL,
    visits_included_snapshot integer NOT NULL,
    asset_label character varying(150) NOT NULL,
    status character varying(20) NOT NULL,
    start_date_utc timestamp with time zone NOT NULL,
    end_date_utc timestamp with time zone NOT NULL,
    visits_remaining integer NOT NULL,
    payment_transaction_id uuid,
    expiring_soon_notified_for_end_date_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    cancelled_at_utc timestamp with time zone
);


--
-- Name: customer_auth_identity; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_auth_identity (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    provider character varying(20) NOT NULL,
    identifier character varying(200) NOT NULL,
    password_hash character varying(500),
    is_primary boolean NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: customer_communication_preference; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_communication_preference (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    transactional_sms_enabled boolean NOT NULL,
    transactional_email_enabled boolean NOT NULL,
    transactional_whatsapp_enabled boolean NOT NULL,
    promotional_sms_enabled boolean NOT NULL,
    promotional_email_enabled boolean NOT NULL,
    promotional_whatsapp_enabled boolean NOT NULL,
    push_enabled boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: customer_note; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_note (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    author_admin_user_id uuid NOT NULL,
    note character varying(4000) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: customer_otp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_otp (
    id uuid NOT NULL,
    customer_id uuid,
    target character varying(200) NOT NULL,
    purpose character varying(20) NOT NULL,
    code_hash character varying(500) NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    consumed_at timestamp with time zone,
    attempt_count integer NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: customer_rating; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_rating (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    provider_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    rating integer NOT NULL,
    note character varying(500),
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: customer_session; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_session (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    refresh_token_hash character varying(500) NOT NULL,
    issued_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone,
    device_info character varying(500),
    ip_address character varying(64)
);


--
-- Name: customer_subscription; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.customer_subscription (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    plan_id uuid NOT NULL,
    plan_name_snapshot character varying(150) NOT NULL,
    price_snapshot numeric(12,2) NOT NULL,
    billing_cycle_snapshot character varying(20) NOT NULL,
    free_visits_included_snapshot integer NOT NULL,
    discount_percent_snapshot numeric(5,2) NOT NULL,
    priority_slot_flag_snapshot boolean NOT NULL,
    status character varying(20) NOT NULL,
    current_period_start_utc timestamp with time zone NOT NULL,
    current_period_end_utc timestamp with time zone NOT NULL,
    free_visits_remaining integer NOT NULL,
    next_billing_date_utc timestamp with time zone NOT NULL,
    retry_count integer NOT NULL,
    last_payment_failure_reason character varying(500),
    expiring_soon_notified_for_period_end_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    cancelled_at_utc timestamp with time zone
);


--
-- Name: device_token; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.device_token (
    id uuid NOT NULL,
    customer_id uuid,
    platform character varying(10) NOT NULL,
    token character varying(500) NOT NULL,
    is_active boolean NOT NULL,
    registered_at_utc timestamp with time zone NOT NULL,
    revoked_at_utc timestamp with time zone,
    provider_id uuid,
    CONSTRAINT ck_device_token_exactly_one_owner CHECK ((((customer_id IS NOT NULL) AND (provider_id IS NULL)) OR ((customer_id IS NULL) AND (provider_id IS NOT NULL))))
);


--
-- Name: export_job; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.export_job (
    id uuid NOT NULL,
    report_type character varying(30) NOT NULL,
    from_utc timestamp with time zone NOT NULL,
    to_utc timestamp with time zone NOT NULL,
    city character varying(100),
    category_id uuid,
    status character varying(20) NOT NULL,
    requested_by_admin_user_id uuid NOT NULL,
    requested_at_utc timestamp with time zone NOT NULL,
    completed_at_utc timestamp with time zone,
    result_file_name character varying(260),
    result_content bytea,
    error_message character varying(2000)
);


--
-- Name: locality; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.locality (
    id uuid NOT NULL,
    zone_id uuid NOT NULL,
    pincode_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: login_attempt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.login_attempt (
    id uuid NOT NULL,
    identifier character varying(200) NOT NULL,
    succeeded boolean NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL
);


--
-- Name: nestly_coins_program_config; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.nestly_coins_program_config (
    id uuid NOT NULL,
    audience character varying(20) NOT NULL,
    earn_rate_per_100 numeric(12,2) NOT NULL,
    minimum_order_amount numeric(12,2) NOT NULL,
    require_reorder boolean NOT NULL,
    max_coins_per_month numeric(12,2),
    expiry_days integer NOT NULL,
    clawback_window_days integer NOT NULL,
    is_active boolean NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: notification_event; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notification_event (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    booking_id uuid,
    support_ticket_id uuid,
    event_type character varying(30) NOT NULL,
    channel character varying(20) NOT NULL,
    recipient character varying(200) NOT NULL,
    template_key character varying(100) NOT NULL,
    payload_json text,
    status character varying(20) NOT NULL,
    error_reason character varying(1000),
    created_at_utc timestamp with time zone NOT NULL,
    sent_at_utc timestamp with time zone
);


--
-- Name: notification_intent; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notification_intent (
    id uuid NOT NULL,
    dedupe_key character varying(100) NOT NULL,
    domain_event_id uuid NOT NULL,
    domain_event_type character varying(100) NOT NULL,
    payload_json text NOT NULL,
    event_type character varying(30) NOT NULL,
    status character varying(20) NOT NULL,
    attempt_count integer NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    last_attempt_at_utc timestamp with time zone,
    completed_at_utc timestamp with time zone,
    lease_owner character varying(100),
    lease_expires_at_utc timestamp with time zone,
    last_error character varying(1000),
    resolution character varying(500)
);


--
-- Name: notification_template; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.notification_template (
    id uuid NOT NULL,
    event_type character varying(30) NOT NULL,
    channel character varying(20) NOT NULL,
    template_key character varying(100) NOT NULL,
    subject character varying(300),
    body character varying(4000) NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: payment_attempt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.payment_attempt (
    id uuid NOT NULL,
    payment_transaction_id uuid NOT NULL,
    attempt_number integer NOT NULL,
    gateway_order_id character varying(100) NOT NULL,
    gateway_payment_ref character varying(100),
    status character varying(20) NOT NULL,
    failure_reason character varying(500),
    created_at_utc timestamp with time zone NOT NULL,
    completed_at_utc timestamp with time zone
);


--
-- Name: payment_transaction; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.payment_transaction (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    amount numeric(12,2) NOT NULL,
    currency character varying(3) NOT NULL,
    status character varying(20) NOT NULL,
    idempotency_key character varying(100) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    commission_amount numeric(12,2),
    commission_rate_percentage numeric(5,2)
);


--
-- Name: pincode; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.pincode (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    code character varying(10) NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: platform_escrow_ledger; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.platform_escrow_ledger (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    entry_type character varying(20) NOT NULL,
    amount numeric(12,2) NOT NULL,
    balance_after numeric(12,2) NOT NULL,
    source_type character varying(30) NOT NULL,
    source_reference_id uuid,
    provider_id uuid,
    commission_amount numeric(12,2),
    description character varying(300) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: promotional_price; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.promotional_price (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    city_id uuid,
    discounted_price numeric(18,2) NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    is_active boolean DEFAULT true NOT NULL
);


--
-- Name: provider; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider (
    id uuid NOT NULL,
    legal_name character varying(200) NOT NULL,
    display_name character varying(200) NOT NULL,
    provider_type character varying(20) NOT NULL,
    phone character varying(20) NOT NULL,
    email character varying(200),
    status character varying(30) NOT NULL,
    onboarding_status character varying(30) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    latitude numeric(9,6),
    longitude numeric(9,6),
    location_updated_at_utc timestamp with time zone,
    photo_moderated_at_utc timestamp with time zone,
    photo_moderated_by_admin_user_id uuid,
    photo_moderation_note character varying(1000),
    photo_moderation_status character varying(20),
    photo_url character varying(2000),
    referral_code character varying(20)
);


--
-- Name: provider_auth_identity; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_auth_identity (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    provider character varying(20) NOT NULL,
    identifier character varying(200) NOT NULL,
    password_hash character varying(500),
    is_primary boolean NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: provider_availability_window; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_availability_window (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    day_of_week character varying(10) NOT NULL,
    start_time interval NOT NULL,
    end_time interval NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: provider_background_check; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_background_check (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    status character varying(20) NOT NULL,
    checked_by uuid NOT NULL,
    checked_at timestamp with time zone NOT NULL,
    notes character varying(1000)
);


--
-- Name: provider_blackout_date; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_blackout_date (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    reason character varying(500)
);


--
-- Name: provider_capacity; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_capacity (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    max_jobs_per_day integer,
    max_jobs_per_slot integer
);


--
-- Name: provider_earning_ledger; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_earning_ledger (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    entry_type character varying(20) NOT NULL,
    amount numeric(12,2) NOT NULL,
    balance_after numeric(12,2) NOT NULL,
    source_type character varying(30) NOT NULL,
    source_reference_id uuid,
    description character varying(300) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: provider_kyc_document; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_kyc_document (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    doc_type character varying(30) NOT NULL,
    doc_number character varying(100),
    file_ref character varying(1000) NOT NULL,
    verification_status character varying(20) NOT NULL,
    verified_by uuid,
    verified_at timestamp with time zone,
    submitted_at timestamp with time zone NOT NULL
);


--
-- Name: provider_location_ping; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_location_ping (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    booking_id uuid,
    latitude numeric(9,6) NOT NULL,
    longitude numeric(9,6) NOT NULL,
    accuracy_metres numeric(8,1),
    recorded_at_utc timestamp with time zone NOT NULL,
    received_at_utc timestamp with time zone NOT NULL,
    source character varying(20) NOT NULL
);


--
-- Name: provider_login_attempt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_login_attempt (
    id uuid NOT NULL,
    identifier character varying(200) NOT NULL,
    succeeded boolean NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL
);


--
-- Name: provider_otp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_otp (
    id uuid NOT NULL,
    provider_id uuid,
    target character varying(200) NOT NULL,
    purpose character varying(20) NOT NULL,
    code_hash character varying(500) NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    consumed_at timestamp with time zone,
    attempt_count integer NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: provider_payout; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_payout (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    period_start date NOT NULL,
    period_end date NOT NULL,
    total_amount numeric(12,2) NOT NULL,
    status character varying(20) NOT NULL,
    payout_reference character varying(100),
    notes character varying(500),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);


--
-- Name: provider_referral; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_referral (
    id uuid NOT NULL,
    referrer_provider_id uuid NOT NULL,
    referee_provider_id uuid NOT NULL,
    referral_code_used character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    qualifying_booking_id uuid,
    referrer_reward_value numeric(12,2) NOT NULL,
    referee_reward_value numeric(12,2) NOT NULL,
    qualifying_completed_jobs_count integer NOT NULL,
    referrer_earning_entry_id uuid,
    referee_earning_entry_id uuid,
    registered_at_utc timestamp with time zone NOT NULL,
    qualified_at_utc timestamp with time zone,
    rewarded_at_utc timestamp with time zone,
    expires_at_utc timestamp with time zone NOT NULL,
    is_fraud_flagged boolean NOT NULL,
    fraud_review_note character varying(1000),
    fraud_reviewed_by_admin_user_id uuid,
    fraud_reviewed_at_utc timestamp with time zone
);


--
-- Name: provider_referral_program_config; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_referral_program_config (
    id uuid NOT NULL,
    referrer_reward_value numeric(12,2) NOT NULL,
    referee_reward_value numeric(12,2) NOT NULL,
    qualifying_completed_jobs_count integer NOT NULL,
    referral_expiry_days integer NOT NULL,
    max_referrals_per_provider integer,
    is_active boolean NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: provider_service_area; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_service_area (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    city_id uuid NOT NULL,
    zone_id uuid,
    pincode_id uuid,
    is_active boolean NOT NULL
);


--
-- Name: provider_session; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_session (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    refresh_token_hash character varying(500) NOT NULL,
    issued_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone,
    device_info character varying(500),
    ip_address character varying(64)
);


--
-- Name: provider_skill_mapping; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.provider_skill_mapping (
    id uuid NOT NULL,
    provider_id uuid NOT NULL,
    category_id uuid NOT NULL,
    service_id uuid,
    is_active boolean NOT NULL
);


--
-- Name: recurring_booking_occurrence; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recurring_booking_occurrence (
    id uuid NOT NULL,
    recurring_booking_plan_id uuid NOT NULL,
    scheduled_date date NOT NULL,
    outcome character varying(30) NOT NULL,
    booking_id uuid,
    skip_reason character varying(500),
    processed_at_utc timestamp with time zone NOT NULL
);


--
-- Name: recurring_booking_plan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recurring_booking_plan (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    service_id uuid NOT NULL,
    city_id uuid NOT NULL,
    locality_id uuid NOT NULL,
    address_id uuid NOT NULL,
    slot_window_id uuid NOT NULL,
    quantity integer NOT NULL,
    frequency character varying(20) NOT NULL,
    recurrence_day_of_week character varying(20),
    recurrence_day_of_month integer,
    start_date date NOT NULL,
    end_date date,
    occurrence_count integer,
    completed_occurrence_count integer NOT NULL,
    next_occurrence_date date NOT NULL,
    status character varying(20) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    apply_wallet_credit boolean DEFAULT false NOT NULL
);


--
-- Name: recurring_booking_plan_addon; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recurring_booking_plan_addon (
    id uuid NOT NULL,
    recurring_booking_plan_id uuid NOT NULL,
    add_on_id uuid NOT NULL,
    quantity integer NOT NULL
);


--
-- Name: referral; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.referral (
    id uuid NOT NULL,
    referrer_customer_id uuid NOT NULL,
    referee_customer_id uuid NOT NULL,
    referral_code_used character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    qualifying_booking_id uuid,
    referrer_reward_type character varying(20) NOT NULL,
    referrer_reward_value numeric(12,2) NOT NULL,
    referee_reward_type character varying(20) NOT NULL,
    referee_reward_value numeric(12,2) NOT NULL,
    min_qualifying_order_amount numeric(12,2) NOT NULL,
    referrer_wallet_entry_id uuid,
    referrer_coupon_id uuid,
    referee_wallet_entry_id uuid,
    referee_coupon_id uuid,
    registered_at_utc timestamp with time zone NOT NULL,
    qualified_at_utc timestamp with time zone,
    rewarded_at_utc timestamp with time zone,
    expires_at_utc timestamp with time zone NOT NULL,
    fraud_review_note character varying(1000),
    fraud_reviewed_at_utc timestamp with time zone,
    fraud_reviewed_by_admin_user_id uuid,
    is_fraud_flagged boolean DEFAULT false NOT NULL
);


--
-- Name: referral_milestone; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.referral_milestone (
    id uuid NOT NULL,
    threshold_count integer NOT NULL,
    bonus_type character varying(20) NOT NULL,
    bonus_value numeric(12,2) NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);


--
-- Name: referral_milestone_award; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.referral_milestone_award (
    id uuid NOT NULL,
    referral_milestone_id uuid NOT NULL,
    referrer_customer_id uuid NOT NULL,
    wallet_entry_id uuid,
    coupon_id uuid,
    awarded_at_utc timestamp with time zone NOT NULL
);


--
-- Name: referral_program_config; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.referral_program_config (
    id uuid NOT NULL,
    referrer_reward_type character varying(20) NOT NULL,
    referrer_reward_value numeric(12,2) NOT NULL,
    referee_reward_type character varying(20) NOT NULL,
    referee_reward_value numeric(12,2) NOT NULL,
    min_qualifying_order_amount numeric(12,2) NOT NULL,
    referral_expiry_days integer NOT NULL,
    max_referrals_per_customer integer,
    is_active boolean NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: refund_transaction; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.refund_transaction (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    payment_transaction_id uuid,
    type character varying(20) NOT NULL,
    method character varying(20) NOT NULL,
    amount numeric(12,2) NOT NULL,
    status character varying(20) NOT NULL,
    gateway_refund_ref character varying(100),
    reason character varying(500) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    processed_at_utc timestamp with time zone,
    funding_source character varying(20) DEFAULT 'Payment'::character varying NOT NULL
);


--
-- Name: review; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.review (
    id uuid NOT NULL,
    booking_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    service_id uuid NOT NULL,
    rating integer NOT NULL,
    review_text character varying(2000),
    issue_tags character varying(500),
    status character varying(20) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    is_flagged boolean DEFAULT false NOT NULL,
    moderated_at_utc timestamp with time zone,
    moderated_by_admin_user_id uuid,
    moderator_note character varying(1000),
    provider_id uuid
);


--
-- Name: role_permission_mapping; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.role_permission_mapping (
    id uuid NOT NULL,
    role_id uuid NOT NULL,
    permission_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL
);


--
-- Name: service; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service (
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    description character varying(2000) NOT NULL,
    price numeric(18,2) NOT NULL,
    is_active boolean NOT NULL,
    exclusions character varying(4000) DEFAULT ''::character varying NOT NULL,
    inclusions character varying(4000) DEFAULT ''::character varying NOT NULL,
    slug character varying(200) DEFAULT ''::character varying NOT NULL,
    cancellation_policy character varying(2000),
    reschedule_policy character varying(2000),
    duration_minutes integer DEFAULT 60 NOT NULL,
    is_add_on_allowed boolean DEFAULT true NOT NULL,
    is_address_required boolean DEFAULT true NOT NULL,
    is_customer_note_allowed boolean DEFAULT true NOT NULL,
    is_featured boolean DEFAULT false NOT NULL,
    is_inspection_based boolean DEFAULT false NOT NULL,
    is_quantity_allowed boolean DEFAULT false NOT NULL,
    is_slot_required boolean DEFAULT true NOT NULL,
    is_tax_applicable boolean DEFAULT true NOT NULL,
    pricing_type character varying(20) DEFAULT 'Fixed'::character varying NOT NULL,
    seo_meta_description character varying(500),
    seo_title character varying(200),
    short_description character varying(500),
    sort_order integer DEFAULT 0 NOT NULL,
    cover_image_url character varying(500),
    service_group_id uuid,
    is_duration_based boolean DEFAULT false NOT NULL
);


--
-- Name: service_add_on_group; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_add_on_group (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    selection_type character varying(20) NOT NULL,
    min_select integer DEFAULT 0 NOT NULL,
    max_select integer,
    sort_order integer DEFAULT 0 NOT NULL
);


--
-- Name: service_addon; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_addon (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    price numeric(18,2) NOT NULL,
    description character varying(1000),
    is_active boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    is_mandatory boolean DEFAULT false NOT NULL,
    is_quantity_allowed boolean DEFAULT false NOT NULL,
    group_id uuid
);


--
-- Name: service_city_price; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_city_price (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    city_id uuid NOT NULL,
    price numeric(18,2) NOT NULL,
    effective_end_date date,
    effective_start_date date DEFAULT CURRENT_DATE NOT NULL
);


--
-- Name: service_faq; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_faq (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    question character varying(500) NOT NULL,
    answer character varying(2000) NOT NULL
);


--
-- Name: service_group; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_group (
    id uuid NOT NULL,
    category_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL
);


--
-- Name: service_media; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_media (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    url character varying(1000) NOT NULL
);


--
-- Name: service_pincode_mapping; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_pincode_mapping (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    pincode_id uuid NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: service_variant; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.service_variant (
    id uuid NOT NULL,
    service_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    price numeric(18,2) NOT NULL,
    duration_minutes integer NOT NULL,
    inclusions_override character varying(4000),
    is_active boolean DEFAULT true NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL
);


--
-- Name: slot_availability_override; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_availability_override (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    date date NOT NULL,
    slot_window_id uuid,
    category_id uuid,
    service_id uuid,
    reason character varying(500) NOT NULL
);


--
-- Name: slot_blackout; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_blackout (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    type character varying(20) NOT NULL,
    reason character varying(500)
);


--
-- Name: slot_booking_counter; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_booking_counter (
    id uuid NOT NULL,
    slot_window_id uuid NOT NULL,
    slot_date date NOT NULL,
    booked_count integer NOT NULL
);


--
-- Name: slot_booking_policy; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_booking_policy (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    cutoff_minutes integer NOT NULL,
    max_advance_days integer NOT NULL
);


--
-- Name: slot_window; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_window (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    start_time interval NOT NULL,
    end_time interval NOT NULL,
    is_active boolean NOT NULL,
    max_bookings_per_slot integer
);


--
-- Name: slot_window_rule; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.slot_window_rule (
    id uuid NOT NULL,
    slot_window_id uuid NOT NULL,
    day_of_week integer NOT NULL
);


--
-- Name: state; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.state (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    code character varying(10) NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: subscription_plan; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.subscription_plan (
    id uuid NOT NULL,
    name character varying(150) NOT NULL,
    description character varying(500),
    price numeric(12,2) NOT NULL,
    billing_cycle character varying(20) NOT NULL,
    free_visits_included integer NOT NULL,
    discount_percent numeric(5,2) NOT NULL,
    priority_slot_flag boolean NOT NULL,
    is_active boolean NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: support_ticket; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.support_ticket (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    booking_id uuid,
    category character varying(30) NOT NULL,
    priority character varying(20) NOT NULL,
    subject character varying(200) NOT NULL,
    description character varying(4000) NOT NULL,
    status character varying(20) NOT NULL,
    resolution_summary character varying(2000),
    is_disputed boolean NOT NULL,
    dispute_outcome character varying(20),
    dispute_resolved_at_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    assigned_admin_user_id uuid,
    assigned_at_utc timestamp with time zone,
    escalated_at_utc timestamp with time zone
);


--
-- Name: support_ticket_comment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.support_ticket_comment (
    id uuid NOT NULL,
    support_ticket_id uuid NOT NULL,
    comment character varying(2000) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    author_type character varying(20) DEFAULT ''::character varying NOT NULL
);


--
-- Name: system_setting; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.system_setting (
    id uuid NOT NULL,
    group_key character varying(50) NOT NULL,
    value_json jsonb NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    updated_by_admin_user_id uuid
);


--
-- Name: wallet_ledger; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.wallet_ledger (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    entry_type character varying(20) NOT NULL,
    amount numeric(12,2) NOT NULL,
    balance_after numeric(12,2) NOT NULL,
    source_type character varying(30) NOT NULL,
    source_reference_id uuid,
    description character varying(300) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone,
    remaining_amount numeric(12,2)
);


--
-- Name: zone; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.zone (
    id uuid NOT NULL,
    city_id uuid NOT NULL,
    name character varying(200) NOT NULL,
    is_active boolean NOT NULL
);


--
-- Name: aggregatedcounter id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.aggregatedcounter ALTER COLUMN id SET DEFAULT nextval('hangfire.aggregatedcounter_id_seq'::regclass);


--
-- Name: counter id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.counter ALTER COLUMN id SET DEFAULT nextval('hangfire.counter_id_seq'::regclass);


--
-- Name: hash id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.hash ALTER COLUMN id SET DEFAULT nextval('hangfire.hash_id_seq'::regclass);


--
-- Name: job id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.job ALTER COLUMN id SET DEFAULT nextval('hangfire.job_id_seq'::regclass);


--
-- Name: jobparameter id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.jobparameter ALTER COLUMN id SET DEFAULT nextval('hangfire.jobparameter_id_seq'::regclass);


--
-- Name: jobqueue id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.jobqueue ALTER COLUMN id SET DEFAULT nextval('hangfire.jobqueue_id_seq'::regclass);


--
-- Name: list id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.list ALTER COLUMN id SET DEFAULT nextval('hangfire.list_id_seq'::regclass);


--
-- Name: set id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.set ALTER COLUMN id SET DEFAULT nextval('hangfire.set_id_seq'::regclass);


--
-- Name: state id; Type: DEFAULT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.state ALTER COLUMN id SET DEFAULT nextval('hangfire.state_id_seq'::regclass);


--
-- Name: aggregatedcounter aggregatedcounter_key_key; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.aggregatedcounter
    ADD CONSTRAINT aggregatedcounter_key_key UNIQUE (key);


--
-- Name: aggregatedcounter aggregatedcounter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.aggregatedcounter
    ADD CONSTRAINT aggregatedcounter_pkey PRIMARY KEY (id);


--
-- Name: counter counter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.counter
    ADD CONSTRAINT counter_pkey PRIMARY KEY (id);


--
-- Name: hash hash_key_field_key; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.hash
    ADD CONSTRAINT hash_key_field_key UNIQUE (key, field);


--
-- Name: hash hash_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.hash
    ADD CONSTRAINT hash_pkey PRIMARY KEY (id);


--
-- Name: job job_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.job
    ADD CONSTRAINT job_pkey PRIMARY KEY (id);


--
-- Name: jobparameter jobparameter_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.jobparameter
    ADD CONSTRAINT jobparameter_pkey PRIMARY KEY (id);


--
-- Name: jobqueue jobqueue_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.jobqueue
    ADD CONSTRAINT jobqueue_pkey PRIMARY KEY (id);


--
-- Name: list list_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.list
    ADD CONSTRAINT list_pkey PRIMARY KEY (id);


--
-- Name: lock lock_resource_key; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.lock
    ADD CONSTRAINT lock_resource_key UNIQUE (resource);

ALTER TABLE ONLY hangfire.lock REPLICA IDENTITY USING INDEX lock_resource_key;


--
-- Name: schema schema_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.schema
    ADD CONSTRAINT schema_pkey PRIMARY KEY (version);


--
-- Name: server server_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.server
    ADD CONSTRAINT server_pkey PRIMARY KEY (id);


--
-- Name: set set_key_value_key; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.set
    ADD CONSTRAINT set_key_value_key UNIQUE (key, value);


--
-- Name: set set_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.set
    ADD CONSTRAINT set_pkey PRIMARY KEY (id);


--
-- Name: state state_pkey; Type: CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.state
    ADD CONSTRAINT state_pkey PRIMARY KEY (id);


--
-- Name: booking ex_booking_provider_no_double_booking; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT ex_booking_provider_no_double_booking EXCLUDE USING gist (assigned_provider_id WITH =, tsrange((slot_date + slot_start_time_snapshot), (slot_date + slot_end_time_snapshot), '[)'::text) WITH &&) WHERE (((assigned_provider_id IS NOT NULL) AND (slot_end_time_snapshot > slot_start_time_snapshot) AND ((status)::text = ANY ((ARRAY['Assigned'::character varying, 'ProviderEnRoute'::character varying, 'ProviderArrived'::character varying, 'InProgress'::character varying, 'Completed'::character varying])::text[]))));


--
-- Name: __EFMigrationsHistory pk___ef_migrations_history; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id);


--
-- Name: admin_permission pk_admin_permission; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.admin_permission
    ADD CONSTRAINT pk_admin_permission PRIMARY KEY (id);


--
-- Name: admin_role pk_admin_role; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.admin_role
    ADD CONSTRAINT pk_admin_role PRIMARY KEY (id);


--
-- Name: admin_user pk_admin_user; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.admin_user
    ADD CONSTRAINT pk_admin_user PRIMARY KEY (id);


--
-- Name: amc_plan pk_amc_plan; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.amc_plan
    ADD CONSTRAINT pk_amc_plan PRIMARY KEY (id);


--
-- Name: amc_service_visit pk_amc_service_visit; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.amc_service_visit
    ADD CONSTRAINT pk_amc_service_visit PRIMARY KEY (id);


--
-- Name: audit_log pk_audit_log; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.audit_log
    ADD CONSTRAINT pk_audit_log PRIMARY KEY (id);


--
-- Name: banner pk_banner; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.banner
    ADD CONSTRAINT pk_banner PRIMARY KEY (id);


--
-- Name: booking pk_booking; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT pk_booking PRIMARY KEY (id);


--
-- Name: booking_addon_item pk_booking_addon_item; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_addon_item
    ADD CONSTRAINT pk_booking_addon_item PRIMARY KEY (id);


--
-- Name: booking_cancellation pk_booking_cancellation; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_cancellation
    ADD CONSTRAINT pk_booking_cancellation PRIMARY KEY (id);


--
-- Name: booking_completion_proof pk_booking_completion_proof; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_completion_proof
    ADD CONSTRAINT pk_booking_completion_proof PRIMARY KEY (id);


--
-- Name: booking_item pk_booking_item; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_item
    ADD CONSTRAINT pk_booking_item PRIMARY KEY (id);


--
-- Name: booking_provider_assignment pk_booking_provider_assignment; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_provider_assignment
    ADD CONSTRAINT pk_booking_provider_assignment PRIMARY KEY (id);


--
-- Name: booking_reschedule pk_booking_reschedule; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_reschedule
    ADD CONSTRAINT pk_booking_reschedule PRIMARY KEY (id);


--
-- Name: booking_status_history pk_booking_status_history; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_status_history
    ADD CONSTRAINT pk_booking_status_history PRIMARY KEY (id);


--
-- Name: booking_tracking pk_booking_tracking; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_tracking
    ADD CONSTRAINT pk_booking_tracking PRIMARY KEY (id);


--
-- Name: category pk_category; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.category
    ADD CONSTRAINT pk_category PRIMARY KEY (id);


--
-- Name: category_city_mapping pk_category_city_mapping; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.category_city_mapping
    ADD CONSTRAINT pk_category_city_mapping PRIMARY KEY (id);


--
-- Name: chat_message pk_chat_message; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.chat_message
    ADD CONSTRAINT pk_chat_message PRIMARY KEY (id);


--
-- Name: chat_thread pk_chat_thread; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.chat_thread
    ADD CONSTRAINT pk_chat_thread PRIMARY KEY (id);


--
-- Name: city pk_city; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.city
    ADD CONSTRAINT pk_city PRIMARY KEY (id);


--
-- Name: city_pricing_policy pk_city_pricing_policy; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.city_pricing_policy
    ADD CONSTRAINT pk_city_pricing_policy PRIMARY KEY (id);


--
-- Name: cms_faq pk_cms_faq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cms_faq
    ADD CONSTRAINT pk_cms_faq PRIMARY KEY (id);


--
-- Name: cms_media pk_cms_media; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cms_media
    ADD CONSTRAINT pk_cms_media PRIMARY KEY (id);


--
-- Name: cms_page pk_cms_page; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cms_page
    ADD CONSTRAINT pk_cms_page PRIMARY KEY (id);


--
-- Name: coupon pk_coupon; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon
    ADD CONSTRAINT pk_coupon PRIMARY KEY (id);


--
-- Name: coupon_customer_redemption_counter pk_coupon_customer_redemption_counter; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_customer_redemption_counter
    ADD CONSTRAINT pk_coupon_customer_redemption_counter PRIMARY KEY (id);


--
-- Name: coupon_redemption pk_coupon_redemption; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_redemption
    ADD CONSTRAINT pk_coupon_redemption PRIMARY KEY (id);


--
-- Name: customer pk_customer; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer
    ADD CONSTRAINT pk_customer PRIMARY KEY (id);


--
-- Name: customer_address pk_customer_address; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_address
    ADD CONSTRAINT pk_customer_address PRIMARY KEY (id);


--
-- Name: customer_amc_contract pk_customer_amc_contract; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_amc_contract
    ADD CONSTRAINT pk_customer_amc_contract PRIMARY KEY (id);


--
-- Name: customer_auth_identity pk_customer_auth_identity; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_auth_identity
    ADD CONSTRAINT pk_customer_auth_identity PRIMARY KEY (id);


--
-- Name: customer_communication_preference pk_customer_communication_preference; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_communication_preference
    ADD CONSTRAINT pk_customer_communication_preference PRIMARY KEY (id);


--
-- Name: customer_note pk_customer_note; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_note
    ADD CONSTRAINT pk_customer_note PRIMARY KEY (id);


--
-- Name: customer_otp pk_customer_otp; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_otp
    ADD CONSTRAINT pk_customer_otp PRIMARY KEY (id);


--
-- Name: customer_rating pk_customer_rating; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_rating
    ADD CONSTRAINT pk_customer_rating PRIMARY KEY (id);


--
-- Name: customer_session pk_customer_session; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_session
    ADD CONSTRAINT pk_customer_session PRIMARY KEY (id);


--
-- Name: customer_subscription pk_customer_subscription; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_subscription
    ADD CONSTRAINT pk_customer_subscription PRIMARY KEY (id);


--
-- Name: device_token pk_device_token; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.device_token
    ADD CONSTRAINT pk_device_token PRIMARY KEY (id);


--
-- Name: export_job pk_export_job; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.export_job
    ADD CONSTRAINT pk_export_job PRIMARY KEY (id);


--
-- Name: locality pk_locality; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.locality
    ADD CONSTRAINT pk_locality PRIMARY KEY (id);


--
-- Name: login_attempt pk_login_attempt; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.login_attempt
    ADD CONSTRAINT pk_login_attempt PRIMARY KEY (id);


--
-- Name: nestly_coins_program_config pk_nestly_coins_program_config; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.nestly_coins_program_config
    ADD CONSTRAINT pk_nestly_coins_program_config PRIMARY KEY (id);


--
-- Name: notification_event pk_notification_event; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_event
    ADD CONSTRAINT pk_notification_event PRIMARY KEY (id);


--
-- Name: notification_intent pk_notification_intent; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_intent
    ADD CONSTRAINT pk_notification_intent PRIMARY KEY (id);


--
-- Name: notification_template pk_notification_template; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_template
    ADD CONSTRAINT pk_notification_template PRIMARY KEY (id);


--
-- Name: payment_attempt pk_payment_attempt; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_attempt
    ADD CONSTRAINT pk_payment_attempt PRIMARY KEY (id);


--
-- Name: payment_transaction pk_payment_transaction; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_transaction
    ADD CONSTRAINT pk_payment_transaction PRIMARY KEY (id);


--
-- Name: pincode pk_pincode; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pincode
    ADD CONSTRAINT pk_pincode PRIMARY KEY (id);


--
-- Name: platform_escrow_ledger pk_platform_escrow_ledger; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.platform_escrow_ledger
    ADD CONSTRAINT pk_platform_escrow_ledger PRIMARY KEY (id);


--
-- Name: promotional_price pk_promotional_price; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotional_price
    ADD CONSTRAINT pk_promotional_price PRIMARY KEY (id);


--
-- Name: provider pk_provider; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider
    ADD CONSTRAINT pk_provider PRIMARY KEY (id);


--
-- Name: provider_auth_identity pk_provider_auth_identity; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_auth_identity
    ADD CONSTRAINT pk_provider_auth_identity PRIMARY KEY (id);


--
-- Name: provider_availability_window pk_provider_availability_window; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_availability_window
    ADD CONSTRAINT pk_provider_availability_window PRIMARY KEY (id);


--
-- Name: provider_background_check pk_provider_background_check; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_background_check
    ADD CONSTRAINT pk_provider_background_check PRIMARY KEY (id);


--
-- Name: provider_blackout_date pk_provider_blackout_date; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_blackout_date
    ADD CONSTRAINT pk_provider_blackout_date PRIMARY KEY (id);


--
-- Name: provider_capacity pk_provider_capacity; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_capacity
    ADD CONSTRAINT pk_provider_capacity PRIMARY KEY (id);


--
-- Name: provider_earning_ledger pk_provider_earning_ledger; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_earning_ledger
    ADD CONSTRAINT pk_provider_earning_ledger PRIMARY KEY (id);


--
-- Name: provider_kyc_document pk_provider_kyc_document; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_kyc_document
    ADD CONSTRAINT pk_provider_kyc_document PRIMARY KEY (id);


--
-- Name: provider_location_ping pk_provider_location_ping; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_location_ping
    ADD CONSTRAINT pk_provider_location_ping PRIMARY KEY (id);


--
-- Name: provider_login_attempt pk_provider_login_attempt; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_login_attempt
    ADD CONSTRAINT pk_provider_login_attempt PRIMARY KEY (id);


--
-- Name: provider_otp pk_provider_otp; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_otp
    ADD CONSTRAINT pk_provider_otp PRIMARY KEY (id);


--
-- Name: provider_payout pk_provider_payout; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_payout
    ADD CONSTRAINT pk_provider_payout PRIMARY KEY (id);


--
-- Name: provider_referral pk_provider_referral; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_referral
    ADD CONSTRAINT pk_provider_referral PRIMARY KEY (id);


--
-- Name: provider_referral_program_config pk_provider_referral_program_config; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_referral_program_config
    ADD CONSTRAINT pk_provider_referral_program_config PRIMARY KEY (id);


--
-- Name: provider_service_area pk_provider_service_area; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_service_area
    ADD CONSTRAINT pk_provider_service_area PRIMARY KEY (id);


--
-- Name: provider_session pk_provider_session; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_session
    ADD CONSTRAINT pk_provider_session PRIMARY KEY (id);


--
-- Name: provider_skill_mapping pk_provider_skill_mapping; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_skill_mapping
    ADD CONSTRAINT pk_provider_skill_mapping PRIMARY KEY (id);


--
-- Name: recurring_booking_occurrence pk_recurring_booking_occurrence; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_occurrence
    ADD CONSTRAINT pk_recurring_booking_occurrence PRIMARY KEY (id);


--
-- Name: recurring_booking_plan pk_recurring_booking_plan; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT pk_recurring_booking_plan PRIMARY KEY (id);


--
-- Name: recurring_booking_plan_addon pk_recurring_booking_plan_addon; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan_addon
    ADD CONSTRAINT pk_recurring_booking_plan_addon PRIMARY KEY (id);


--
-- Name: referral pk_referral; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.referral
    ADD CONSTRAINT pk_referral PRIMARY KEY (id);


--
-- Name: referral_milestone pk_referral_milestone; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.referral_milestone
    ADD CONSTRAINT pk_referral_milestone PRIMARY KEY (id);


--
-- Name: referral_milestone_award pk_referral_milestone_award; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.referral_milestone_award
    ADD CONSTRAINT pk_referral_milestone_award PRIMARY KEY (id);


--
-- Name: referral_program_config pk_referral_program_config; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.referral_program_config
    ADD CONSTRAINT pk_referral_program_config PRIMARY KEY (id);


--
-- Name: refund_transaction pk_refund_transaction; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.refund_transaction
    ADD CONSTRAINT pk_refund_transaction PRIMARY KEY (id);


--
-- Name: review pk_review; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.review
    ADD CONSTRAINT pk_review PRIMARY KEY (id);


--
-- Name: role_permission_mapping pk_role_permission_mapping; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_permission_mapping
    ADD CONSTRAINT pk_role_permission_mapping PRIMARY KEY (id);


--
-- Name: service pk_service; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service
    ADD CONSTRAINT pk_service PRIMARY KEY (id);


--
-- Name: service_add_on_group pk_service_add_on_group; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_add_on_group
    ADD CONSTRAINT pk_service_add_on_group PRIMARY KEY (id);


--
-- Name: service_addon pk_service_addon; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_addon
    ADD CONSTRAINT pk_service_addon PRIMARY KEY (id);


--
-- Name: service_city_price pk_service_city_price; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_city_price
    ADD CONSTRAINT pk_service_city_price PRIMARY KEY (id);


--
-- Name: service_faq pk_service_faq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_faq
    ADD CONSTRAINT pk_service_faq PRIMARY KEY (id);


--
-- Name: service_group pk_service_group; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_group
    ADD CONSTRAINT pk_service_group PRIMARY KEY (id);


--
-- Name: service_media pk_service_media; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_media
    ADD CONSTRAINT pk_service_media PRIMARY KEY (id);


--
-- Name: service_pincode_mapping pk_service_pincode_mapping; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_pincode_mapping
    ADD CONSTRAINT pk_service_pincode_mapping PRIMARY KEY (id);


--
-- Name: service_variant pk_service_variant; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_variant
    ADD CONSTRAINT pk_service_variant PRIMARY KEY (id);


--
-- Name: slot_availability_override pk_slot_availability_override; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_availability_override
    ADD CONSTRAINT pk_slot_availability_override PRIMARY KEY (id);


--
-- Name: slot_blackout pk_slot_blackout; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_blackout
    ADD CONSTRAINT pk_slot_blackout PRIMARY KEY (id);


--
-- Name: slot_booking_counter pk_slot_booking_counter; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_booking_counter
    ADD CONSTRAINT pk_slot_booking_counter PRIMARY KEY (id);


--
-- Name: slot_booking_policy pk_slot_booking_policy; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_booking_policy
    ADD CONSTRAINT pk_slot_booking_policy PRIMARY KEY (id);


--
-- Name: slot_window pk_slot_window; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_window
    ADD CONSTRAINT pk_slot_window PRIMARY KEY (id);


--
-- Name: slot_window_rule pk_slot_window_rule; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_window_rule
    ADD CONSTRAINT pk_slot_window_rule PRIMARY KEY (id);


--
-- Name: state pk_state; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.state
    ADD CONSTRAINT pk_state PRIMARY KEY (id);


--
-- Name: subscription_plan pk_subscription_plan; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.subscription_plan
    ADD CONSTRAINT pk_subscription_plan PRIMARY KEY (id);


--
-- Name: support_ticket pk_support_ticket; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket
    ADD CONSTRAINT pk_support_ticket PRIMARY KEY (id);


--
-- Name: support_ticket_comment pk_support_ticket_comment; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket_comment
    ADD CONSTRAINT pk_support_ticket_comment PRIMARY KEY (id);


--
-- Name: system_setting pk_system_setting; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.system_setting
    ADD CONSTRAINT pk_system_setting PRIMARY KEY (id);


--
-- Name: wallet_ledger pk_wallet_ledger; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.wallet_ledger
    ADD CONSTRAINT pk_wallet_ledger PRIMARY KEY (id);


--
-- Name: zone pk_zone; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.zone
    ADD CONSTRAINT pk_zone PRIMARY KEY (id);


--
-- Name: ix_hangfire_counter_expireat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_counter_expireat ON hangfire.counter USING btree (expireat);


--
-- Name: ix_hangfire_counter_key; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_counter_key ON hangfire.counter USING btree (key);


--
-- Name: ix_hangfire_hash_expireat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_hash_expireat ON hangfire.hash USING btree (expireat);


--
-- Name: ix_hangfire_job_expireat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_job_expireat ON hangfire.job USING btree (expireat);


--
-- Name: ix_hangfire_job_statename; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_job_statename ON hangfire.job USING btree (statename);


--
-- Name: ix_hangfire_job_statename_is_not_null; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_job_statename_is_not_null ON hangfire.job USING btree (statename) INCLUDE (id) WHERE (statename IS NOT NULL);


--
-- Name: ix_hangfire_jobparameter_jobidandname; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_jobparameter_jobidandname ON hangfire.jobparameter USING btree (jobid, name);


--
-- Name: ix_hangfire_jobqueue_fetchedat_queue_jobid; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_jobqueue_fetchedat_queue_jobid ON hangfire.jobqueue USING btree (fetchedat NULLS FIRST, queue, jobid);


--
-- Name: ix_hangfire_jobqueue_jobidandqueue; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_jobqueue_jobidandqueue ON hangfire.jobqueue USING btree (jobid, queue);


--
-- Name: ix_hangfire_jobqueue_queueandfetchedat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_jobqueue_queueandfetchedat ON hangfire.jobqueue USING btree (queue, fetchedat);


--
-- Name: ix_hangfire_list_expireat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_list_expireat ON hangfire.list USING btree (expireat);


--
-- Name: ix_hangfire_set_expireat; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_set_expireat ON hangfire.set USING btree (expireat);


--
-- Name: ix_hangfire_set_key_score; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_set_key_score ON hangfire.set USING btree (key, score);


--
-- Name: ix_hangfire_state_jobid; Type: INDEX; Schema: hangfire; Owner: -
--

CREATE INDEX ix_hangfire_state_jobid ON hangfire.state USING btree (jobid);


--
-- Name: ix_admin_permission_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_admin_permission_code ON public.admin_permission USING btree (code);


--
-- Name: ix_admin_permission_module; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_admin_permission_module ON public.admin_permission USING btree (module);


--
-- Name: ix_admin_role_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_admin_role_name ON public.admin_role USING btree (name);


--
-- Name: ix_admin_user_email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_admin_user_email ON public.admin_user USING btree (email);


--
-- Name: ix_admin_user_role_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_admin_user_role_id ON public.admin_user USING btree (role_id);


--
-- Name: ix_amc_plan_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_amc_plan_category_id ON public.amc_plan USING btree (category_id);


--
-- Name: ix_amc_plan_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_amc_plan_name ON public.amc_plan USING btree (name);


--
-- Name: ix_amc_service_visit_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_amc_service_visit_booking_id ON public.amc_service_visit USING btree (booking_id);


--
-- Name: ix_amc_service_visit_contract_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_amc_service_visit_contract_id ON public.amc_service_visit USING btree (contract_id);


--
-- Name: ix_audit_log_actor_type_actor_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_audit_log_actor_type_actor_id ON public.audit_log USING btree (actor_type, actor_id);


--
-- Name: ix_audit_log_entity_name_entity_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_audit_log_entity_name_entity_id ON public.audit_log USING btree (entity_name, entity_id);


--
-- Name: ix_audit_log_occurred_on_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_audit_log_occurred_on_utc ON public.audit_log USING btree (occurred_on_utc DESC);


--
-- Name: ix_banner_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_banner_category_id ON public.banner USING btree (category_id);


--
-- Name: ix_banner_media_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_banner_media_id ON public.banner USING btree (media_id);


--
-- Name: ix_banner_placement_status_sort_order; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_banner_placement_status_sort_order ON public.banner USING btree (placement, status, sort_order);


--
-- Name: ix_booking_addon_item_booking_item_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_addon_item_booking_item_id ON public.booking_addon_item USING btree (booking_item_id);


--
-- Name: ix_booking_amc_contract_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_amc_contract_id ON public.booking USING btree (amc_contract_id);


--
-- Name: ix_booking_assigned_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_assigned_provider_id ON public.booking USING btree (assigned_provider_id);


--
-- Name: ix_booking_booking_reference; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_booking_booking_reference ON public.booking USING btree (booking_reference);


--
-- Name: ix_booking_cancellation_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_booking_cancellation_booking_id ON public.booking_cancellation USING btree (booking_id);


--
-- Name: ix_booking_completion_proof_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_booking_completion_proof_booking_id ON public.booking_completion_proof USING btree (booking_id);


--
-- Name: ix_booking_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_created_at_utc ON public.booking USING btree (created_at_utc);


--
-- Name: ix_booking_customer_id_idempotency_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_booking_customer_id_idempotency_key ON public.booking USING btree (customer_id, idempotency_key);


--
-- Name: ix_booking_customer_id_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_customer_id_status ON public.booking USING btree (customer_id, status);


--
-- Name: ix_booking_item_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_item_booking_id ON public.booking_item USING btree (booking_id);


--
-- Name: ix_booking_provider_assignment_booking_id_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_provider_assignment_booking_id_status ON public.booking_provider_assignment USING btree (booking_id, status);


--
-- Name: ix_booking_provider_assignment_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_provider_assignment_provider_id ON public.booking_provider_assignment USING btree (provider_id);


--
-- Name: ix_booking_recurring_booking_plan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_recurring_booking_plan_id ON public.booking USING btree (recurring_booking_plan_id);


--
-- Name: ix_booking_reschedule_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_reschedule_booking_id ON public.booking_reschedule USING btree (booking_id);


--
-- Name: ix_booking_status_history_booking_id_changed_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_status_history_booking_id_changed_at_utc ON public.booking_status_history USING btree (booking_id, changed_at_utc);


--
-- Name: ix_booking_status_slot_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_booking_status_slot_date ON public.booking USING btree (status, slot_date);


--
-- Name: ix_booking_tracking_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_booking_tracking_booking_id ON public.booking_tracking USING btree (booking_id);


--
-- Name: ix_category_city_mapping_category_id_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_category_city_mapping_category_id_city_id ON public.category_city_mapping USING btree (category_id, city_id);


--
-- Name: ix_category_city_mapping_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_category_city_mapping_city_id ON public.category_city_mapping USING btree (city_id);


--
-- Name: ix_category_parent_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_category_parent_category_id ON public.category USING btree (parent_category_id);


--
-- Name: ix_category_slug; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_category_slug ON public.category USING btree (slug);


--
-- Name: ix_chat_message_thread_id_read_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_chat_message_thread_id_read_at_utc ON public.chat_message USING btree (thread_id, read_at_utc);


--
-- Name: ix_chat_message_thread_id_sent_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_chat_message_thread_id_sent_at_utc ON public.chat_message USING btree (thread_id, sent_at_utc);


--
-- Name: ix_chat_thread_context_type_context_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_chat_thread_context_type_context_id ON public.chat_thread USING btree (context_type, context_id);


--
-- Name: ix_city_pricing_policy_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_city_pricing_policy_city_id ON public.city_pricing_policy USING btree (city_id);


--
-- Name: ix_city_state_id_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_city_state_id_name ON public.city USING btree (state_id, name);


--
-- Name: ix_cms_faq_placement_status_sort_order; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cms_faq_placement_status_sort_order ON public.cms_faq USING btree (placement, status, sort_order);


--
-- Name: ix_cms_page_slug; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_cms_page_slug ON public.cms_page USING btree (slug);


--
-- Name: ix_cms_page_status_placement; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cms_page_status_placement ON public.cms_page USING btree (status, placement);


--
-- Name: ix_coupon_applicable_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_coupon_applicable_category_id ON public.coupon USING btree (applicable_category_id);


--
-- Name: ix_coupon_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_coupon_code ON public.coupon USING btree (code);


--
-- Name: ix_coupon_customer_redemption_counter_coupon_id_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_coupon_customer_redemption_counter_coupon_id_customer_id ON public.coupon_customer_redemption_counter USING btree (coupon_id, customer_id);


--
-- Name: ix_coupon_customer_redemption_counter_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_coupon_customer_redemption_counter_customer_id ON public.coupon_customer_redemption_counter USING btree (customer_id);


--
-- Name: ix_coupon_redemption_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_coupon_redemption_booking_id ON public.coupon_redemption USING btree (booking_id);


--
-- Name: ix_coupon_redemption_coupon_id_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_coupon_redemption_coupon_id_customer_id ON public.coupon_redemption USING btree (coupon_id, customer_id);


--
-- Name: ix_coupon_redemption_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_coupon_redemption_customer_id ON public.coupon_redemption USING btree (customer_id);


--
-- Name: ix_coupon_valid_from_utc_valid_to_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_coupon_valid_from_utc_valid_to_utc ON public.coupon USING btree (valid_from_utc, valid_to_utc);


--
-- Name: ix_customer_address_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_address_customer_id ON public.customer_address USING btree (customer_id);


--
-- Name: ix_customer_address_customer_id1; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_address_customer_id1 ON public.customer_address USING btree (customer_id) WHERE (is_default = true);


--
-- Name: ix_customer_address_locality_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_address_locality_id ON public.customer_address USING btree (locality_id);


--
-- Name: ix_customer_address_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_address_pincode_id ON public.customer_address USING btree (pincode_id);


--
-- Name: ix_customer_amc_contract_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_amc_contract_customer_id ON public.customer_amc_contract USING btree (customer_id);


--
-- Name: ix_customer_amc_contract_end_date_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_amc_contract_end_date_utc ON public.customer_amc_contract USING btree (end_date_utc);


--
-- Name: ix_customer_amc_contract_payment_transaction_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_amc_contract_payment_transaction_id ON public.customer_amc_contract USING btree (payment_transaction_id);


--
-- Name: ix_customer_amc_contract_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_amc_contract_status ON public.customer_amc_contract USING btree (status);


--
-- Name: ix_customer_auth_identity_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_auth_identity_customer_id ON public.customer_auth_identity USING btree (customer_id);


--
-- Name: ix_customer_auth_identity_provider_identifier; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_auth_identity_provider_identifier ON public.customer_auth_identity USING btree (provider, identifier);


--
-- Name: ix_customer_communication_preference_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_communication_preference_customer_id ON public.customer_communication_preference USING btree (customer_id);


--
-- Name: ix_customer_email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_email ON public.customer USING btree (email) WHERE (email IS NOT NULL);


--
-- Name: ix_customer_mobile; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_mobile ON public.customer USING btree (mobile);


--
-- Name: ix_customer_note_customer_id_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_note_customer_id_created_at_utc ON public.customer_note USING btree (customer_id, created_at_utc);


--
-- Name: ix_customer_otp_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_otp_customer_id ON public.customer_otp USING btree (customer_id);


--
-- Name: ix_customer_otp_target; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_otp_target ON public.customer_otp USING btree (target);


--
-- Name: ix_customer_rating_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_rating_booking_id ON public.customer_rating USING btree (booking_id);


--
-- Name: ix_customer_rating_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_rating_customer_id ON public.customer_rating USING btree (customer_id);


--
-- Name: ix_customer_rating_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_rating_provider_id ON public.customer_rating USING btree (provider_id);


--
-- Name: ix_customer_referral_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_referral_code ON public.customer USING btree (referral_code) WHERE (referral_code IS NOT NULL);


--
-- Name: ix_customer_session_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_session_customer_id ON public.customer_session USING btree (customer_id);


--
-- Name: ix_customer_session_refresh_token_hash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_session_refresh_token_hash ON public.customer_session USING btree (refresh_token_hash);


--
-- Name: ix_customer_subscription_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_customer_subscription_customer_id ON public.customer_subscription USING btree (customer_id) WHERE ((status)::text = ANY ((ARRAY['Active'::character varying, 'PaymentFailed'::character varying])::text[]));


--
-- Name: ix_customer_subscription_next_billing_date_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_customer_subscription_next_billing_date_utc ON public.customer_subscription USING btree (next_billing_date_utc);


--
-- Name: ix_device_token_customer_id_is_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_device_token_customer_id_is_active ON public.device_token USING btree (customer_id, is_active);


--
-- Name: ix_device_token_provider_id_is_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_device_token_provider_id_is_active ON public.device_token USING btree (provider_id, is_active);


--
-- Name: ix_device_token_token; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_device_token_token ON public.device_token USING btree (token);


--
-- Name: ix_export_job_requested_by_admin_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_export_job_requested_by_admin_user_id ON public.export_job USING btree (requested_by_admin_user_id);


--
-- Name: ix_locality_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_locality_pincode_id ON public.locality USING btree (pincode_id);


--
-- Name: ix_locality_zone_id_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_locality_zone_id_name ON public.locality USING btree (zone_id, name);


--
-- Name: ix_login_attempt_identifier_occurred_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_login_attempt_identifier_occurred_at_utc ON public.login_attempt USING btree (identifier, occurred_at_utc);


--
-- Name: ix_nestly_coins_program_config_audience; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_nestly_coins_program_config_audience ON public.nestly_coins_program_config USING btree (audience);


--
-- Name: ix_notification_event_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_event_booking_id ON public.notification_event USING btree (booking_id);


--
-- Name: ix_notification_event_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_event_customer_id ON public.notification_event USING btree (customer_id);


--
-- Name: ix_notification_event_event_type_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_event_event_type_status ON public.notification_event USING btree (event_type, status);


--
-- Name: ix_notification_event_support_ticket_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_event_support_ticket_id ON public.notification_event USING btree (support_ticket_id);


--
-- Name: ix_notification_intent_dedupe_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_notification_intent_dedupe_key ON public.notification_intent USING btree (dedupe_key);


--
-- Name: ix_notification_intent_domain_event_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_intent_domain_event_id ON public.notification_intent USING btree (domain_event_id);


--
-- Name: ix_notification_intent_status_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_notification_intent_status_created_at_utc ON public.notification_intent USING btree (status, created_at_utc);


--
-- Name: ix_notification_template_event_type_channel; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_notification_template_event_type_channel ON public.notification_template USING btree (event_type, channel);


--
-- Name: ix_notification_template_template_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_notification_template_template_key ON public.notification_template USING btree (template_key);


--
-- Name: ix_payment_attempt_gateway_order_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_payment_attempt_gateway_order_id ON public.payment_attempt USING btree (gateway_order_id);


--
-- Name: ix_payment_attempt_payment_transaction_id_attempt_number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_payment_attempt_payment_transaction_id_attempt_number ON public.payment_attempt USING btree (payment_transaction_id, attempt_number);


--
-- Name: ix_payment_transaction_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_payment_transaction_booking_id ON public.payment_transaction USING btree (booking_id);


--
-- Name: ix_payment_transaction_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_payment_transaction_customer_id ON public.payment_transaction USING btree (customer_id);


--
-- Name: ix_payment_transaction_idempotency_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_payment_transaction_idempotency_key ON public.payment_transaction USING btree (idempotency_key);


--
-- Name: ix_pincode_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_pincode_city_id ON public.pincode USING btree (city_id);


--
-- Name: ix_pincode_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_pincode_code ON public.pincode USING btree (code);


--
-- Name: ix_platform_escrow_ledger_booking_id_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_platform_escrow_ledger_booking_id_created_at_utc ON public.platform_escrow_ledger USING btree (booking_id, created_at_utc);


--
-- Name: ix_platform_escrow_ledger_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_platform_escrow_ledger_created_at_utc ON public.platform_escrow_ledger USING btree (created_at_utc);


--
-- Name: ix_platform_escrow_ledger_source_reference_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_platform_escrow_ledger_source_reference_id ON public.platform_escrow_ledger USING btree (source_reference_id) WHERE ((entry_type)::text = 'Hold'::text);


--
-- Name: ix_promotional_price_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_promotional_price_city_id ON public.promotional_price USING btree (city_id);


--
-- Name: ix_promotional_price_service_id_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_promotional_price_service_id_city_id ON public.promotional_price USING btree (service_id, city_id);


--
-- Name: ix_provider_auth_identity_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_auth_identity_provider_id ON public.provider_auth_identity USING btree (provider_id);


--
-- Name: ix_provider_auth_identity_provider_identifier; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_auth_identity_provider_identifier ON public.provider_auth_identity USING btree (provider, identifier);


--
-- Name: ix_provider_availability_window_provider_id_day_of_week; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_availability_window_provider_id_day_of_week ON public.provider_availability_window USING btree (provider_id, day_of_week);


--
-- Name: ix_provider_background_check_provider_id_checked_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_background_check_provider_id_checked_at ON public.provider_background_check USING btree (provider_id, checked_at);


--
-- Name: ix_provider_blackout_date_provider_id_start_date_end_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_blackout_date_provider_id_start_date_end_date ON public.provider_blackout_date USING btree (provider_id, start_date, end_date);


--
-- Name: ix_provider_capacity_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_capacity_provider_id ON public.provider_capacity USING btree (provider_id);


--
-- Name: ix_provider_earning_ledger_provider_id_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_earning_ledger_provider_id_created_at_utc ON public.provider_earning_ledger USING btree (provider_id, created_at_utc);


--
-- Name: ix_provider_kyc_document_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_kyc_document_provider_id ON public.provider_kyc_document USING btree (provider_id);


--
-- Name: ix_provider_location_ping_booking_id_recorded_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_location_ping_booking_id_recorded_at_utc ON public.provider_location_ping USING btree (booking_id, recorded_at_utc);


--
-- Name: ix_provider_location_ping_provider_id_recorded_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_location_ping_provider_id_recorded_at_utc ON public.provider_location_ping USING btree (provider_id, recorded_at_utc);


--
-- Name: ix_provider_login_attempt_identifier_occurred_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_login_attempt_identifier_occurred_at_utc ON public.provider_login_attempt USING btree (identifier, occurred_at_utc);


--
-- Name: ix_provider_otp_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_otp_provider_id ON public.provider_otp USING btree (provider_id);


--
-- Name: ix_provider_otp_target; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_otp_target ON public.provider_otp USING btree (target);


--
-- Name: ix_provider_payout_provider_id_period_start_period_end; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_payout_provider_id_period_start_period_end ON public.provider_payout USING btree (provider_id, period_start, period_end);


--
-- Name: ix_provider_payout_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_payout_status ON public.provider_payout USING btree (status);


--
-- Name: ix_provider_phone; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_phone ON public.provider USING btree (phone);


--
-- Name: ix_provider_photo_moderation_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_photo_moderation_status ON public.provider USING btree (photo_moderation_status);


--
-- Name: ix_provider_referral_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_referral_code ON public.provider USING btree (referral_code) WHERE (referral_code IS NOT NULL);


--
-- Name: ix_provider_referral_is_fraud_flagged; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_referral_is_fraud_flagged ON public.provider_referral USING btree (is_fraud_flagged);


--
-- Name: ix_provider_referral_referee_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_referral_referee_provider_id ON public.provider_referral USING btree (referee_provider_id);


--
-- Name: ix_provider_referral_referrer_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_referral_referrer_provider_id ON public.provider_referral USING btree (referrer_provider_id);


--
-- Name: ix_provider_referral_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_referral_status ON public.provider_referral USING btree (status);


--
-- Name: ix_provider_referral_status_expires_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_referral_status_expires_at_utc ON public.provider_referral USING btree (status, expires_at_utc);


--
-- Name: ix_provider_service_area_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_service_area_city_id ON public.provider_service_area USING btree (city_id);


--
-- Name: ix_provider_service_area_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_service_area_pincode_id ON public.provider_service_area USING btree (pincode_id);


--
-- Name: ix_provider_service_area_provider_id_city_id_zone_id_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_service_area_provider_id_city_id_zone_id_pincode_id ON public.provider_service_area USING btree (provider_id, city_id, zone_id, pincode_id);


--
-- Name: ix_provider_service_area_zone_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_service_area_zone_id ON public.provider_service_area USING btree (zone_id);


--
-- Name: ix_provider_session_provider_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_session_provider_id ON public.provider_session USING btree (provider_id);


--
-- Name: ix_provider_session_refresh_token_hash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_session_refresh_token_hash ON public.provider_session USING btree (refresh_token_hash);


--
-- Name: ix_provider_skill_mapping_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_skill_mapping_category_id ON public.provider_skill_mapping USING btree (category_id);


--
-- Name: ix_provider_skill_mapping_provider_id_category_id_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_provider_skill_mapping_provider_id_category_id_service_id ON public.provider_skill_mapping USING btree (provider_id, category_id, service_id);


--
-- Name: ix_provider_skill_mapping_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_provider_skill_mapping_service_id ON public.provider_skill_mapping USING btree (service_id);


--
-- Name: ix_recurring_booking_occurrence_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_recurring_booking_occurrence_booking_id ON public.recurring_booking_occurrence USING btree (booking_id);


--
-- Name: ix_recurring_booking_occurrence_recurring_booking_plan_id_sche; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_recurring_booking_occurrence_recurring_booking_plan_id_sche ON public.recurring_booking_occurrence USING btree (recurring_booking_plan_id, scheduled_date);


--
-- Name: ix_recurring_booking_plan_addon_recurring_booking_plan_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_addon_recurring_booking_plan_id ON public.recurring_booking_plan_addon USING btree (recurring_booking_plan_id);


--
-- Name: ix_recurring_booking_plan_address_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_address_id ON public.recurring_booking_plan USING btree (address_id);


--
-- Name: ix_recurring_booking_plan_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_city_id ON public.recurring_booking_plan USING btree (city_id);


--
-- Name: ix_recurring_booking_plan_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_customer_id ON public.recurring_booking_plan USING btree (customer_id);


--
-- Name: ix_recurring_booking_plan_locality_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_locality_id ON public.recurring_booking_plan USING btree (locality_id);


--
-- Name: ix_recurring_booking_plan_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_service_id ON public.recurring_booking_plan USING btree (service_id);


--
-- Name: ix_recurring_booking_plan_slot_window_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_slot_window_id ON public.recurring_booking_plan USING btree (slot_window_id);


--
-- Name: ix_recurring_booking_plan_status_next_occurrence_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_recurring_booking_plan_status_next_occurrence_date ON public.recurring_booking_plan USING btree (status, next_occurrence_date);


--
-- Name: ix_referral_is_fraud_flagged; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_referral_is_fraud_flagged ON public.referral USING btree (is_fraud_flagged);


--
-- Name: ix_referral_milestone_award_referral_milestone_id_referrer_cus; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_referral_milestone_award_referral_milestone_id_referrer_cus ON public.referral_milestone_award USING btree (referral_milestone_id, referrer_customer_id);


--
-- Name: ix_referral_milestone_threshold_count; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_referral_milestone_threshold_count ON public.referral_milestone USING btree (threshold_count);


--
-- Name: ix_referral_referee_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_referral_referee_customer_id ON public.referral USING btree (referee_customer_id);


--
-- Name: ix_referral_referrer_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_referral_referrer_customer_id ON public.referral USING btree (referrer_customer_id);


--
-- Name: ix_referral_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_referral_status ON public.referral USING btree (status);


--
-- Name: ix_referral_status_expires_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_referral_status_expires_at_utc ON public.referral USING btree (status, expires_at_utc);


--
-- Name: ix_refund_transaction_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_refund_transaction_booking_id ON public.refund_transaction USING btree (booking_id);


--
-- Name: ix_refund_transaction_payment_transaction_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_refund_transaction_payment_transaction_id ON public.refund_transaction USING btree (payment_transaction_id);


--
-- Name: ix_review_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_review_booking_id ON public.review USING btree (booking_id);


--
-- Name: ix_review_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_review_customer_id ON public.review USING btree (customer_id);


--
-- Name: ix_review_provider_id_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_review_provider_id_status ON public.review USING btree (provider_id, status);


--
-- Name: ix_review_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_review_service_id ON public.review USING btree (service_id);


--
-- Name: ix_review_status_is_flagged; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_review_status_is_flagged ON public.review USING btree (status, is_flagged);


--
-- Name: ix_role_permission_mapping_permission_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_role_permission_mapping_permission_id ON public.role_permission_mapping USING btree (permission_id);


--
-- Name: ix_role_permission_mapping_role_id_permission_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_role_permission_mapping_role_id_permission_id ON public.role_permission_mapping USING btree (role_id, permission_id);


--
-- Name: ix_service_add_on_group_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_add_on_group_service_id ON public.service_add_on_group USING btree (service_id);


--
-- Name: ix_service_addon_group_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_addon_group_id ON public.service_addon USING btree (group_id);


--
-- Name: ix_service_addon_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_addon_service_id ON public.service_addon USING btree (service_id);


--
-- Name: ix_service_category_id_is_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_category_id_is_active ON public.service USING btree (category_id, is_active);


--
-- Name: ix_service_city_price_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_city_price_city_id ON public.service_city_price USING btree (city_id);


--
-- Name: ix_service_city_price_service_id_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_service_city_price_service_id_city_id ON public.service_city_price USING btree (service_id, city_id);


--
-- Name: ix_service_faq_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_faq_service_id ON public.service_faq USING btree (service_id);


--
-- Name: ix_service_group_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_group_category_id ON public.service_group USING btree (category_id);


--
-- Name: ix_service_media_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_media_service_id ON public.service_media USING btree (service_id);


--
-- Name: ix_service_pincode_mapping_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_pincode_mapping_pincode_id ON public.service_pincode_mapping USING btree (pincode_id);


--
-- Name: ix_service_pincode_mapping_service_id_pincode_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_service_pincode_mapping_service_id_pincode_id ON public.service_pincode_mapping USING btree (service_id, pincode_id);


--
-- Name: ix_service_service_group_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_service_group_id ON public.service USING btree (service_group_id);


--
-- Name: ix_service_slug; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_service_slug ON public.service USING btree (slug);


--
-- Name: ix_service_variant_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_service_variant_service_id ON public.service_variant USING btree (service_id);


--
-- Name: ix_slot_availability_override_category_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_availability_override_category_id ON public.slot_availability_override USING btree (category_id);


--
-- Name: ix_slot_availability_override_city_id_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_availability_override_city_id_date ON public.slot_availability_override USING btree (city_id, date);


--
-- Name: ix_slot_availability_override_service_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_availability_override_service_id ON public.slot_availability_override USING btree (service_id);


--
-- Name: ix_slot_availability_override_slot_window_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_availability_override_slot_window_id ON public.slot_availability_override USING btree (slot_window_id);


--
-- Name: ix_slot_blackout_city_id_start_date_end_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_blackout_city_id_start_date_end_date ON public.slot_blackout USING btree (city_id, start_date, end_date);


--
-- Name: ix_slot_booking_counter_slot_window_id_slot_date; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_slot_booking_counter_slot_window_id_slot_date ON public.slot_booking_counter USING btree (slot_window_id, slot_date);


--
-- Name: ix_slot_booking_policy_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_slot_booking_policy_city_id ON public.slot_booking_policy USING btree (city_id);


--
-- Name: ix_slot_window_city_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_slot_window_city_id ON public.slot_window USING btree (city_id);


--
-- Name: ix_slot_window_rule_slot_window_id_day_of_week; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_slot_window_rule_slot_window_id_day_of_week ON public.slot_window_rule USING btree (slot_window_id, day_of_week);


--
-- Name: ix_state_code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_state_code ON public.state USING btree (code);


--
-- Name: ix_subscription_plan_is_active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_subscription_plan_is_active ON public.subscription_plan USING btree (is_active);


--
-- Name: ix_subscription_plan_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_subscription_plan_name ON public.subscription_plan USING btree (name);


--
-- Name: ix_support_ticket_assigned_admin_user_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_support_ticket_assigned_admin_user_id ON public.support_ticket USING btree (assigned_admin_user_id);


--
-- Name: ix_support_ticket_booking_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_support_ticket_booking_id ON public.support_ticket USING btree (booking_id);


--
-- Name: ix_support_ticket_comment_support_ticket_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_support_ticket_comment_support_ticket_id ON public.support_ticket_comment USING btree (support_ticket_id);


--
-- Name: ix_support_ticket_customer_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_support_ticket_customer_id ON public.support_ticket USING btree (customer_id);


--
-- Name: ix_support_ticket_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_support_ticket_status ON public.support_ticket USING btree (status);


--
-- Name: ix_system_setting_group_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_system_setting_group_key ON public.system_setting USING btree (group_key);


--
-- Name: ix_wallet_ledger_customer_id_created_at_utc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_wallet_ledger_customer_id_created_at_utc ON public.wallet_ledger USING btree (customer_id, created_at_utc);


--
-- Name: ix_wallet_ledger_expires_at_utc_remaining_amount; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_wallet_ledger_expires_at_utc_remaining_amount ON public.wallet_ledger USING btree (expires_at_utc, remaining_amount);


--
-- Name: ix_zone_city_id_name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_zone_city_id_name ON public.zone USING btree (city_id, name);


--
-- Name: jobparameter jobparameter_jobid_fkey; Type: FK CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.jobparameter
    ADD CONSTRAINT jobparameter_jobid_fkey FOREIGN KEY (jobid) REFERENCES hangfire.job(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: state state_jobid_fkey; Type: FK CONSTRAINT; Schema: hangfire; Owner: -
--

ALTER TABLE ONLY hangfire.state
    ADD CONSTRAINT state_jobid_fkey FOREIGN KEY (jobid) REFERENCES hangfire.job(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: admin_user fk_admin_user_admin_role_role_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.admin_user
    ADD CONSTRAINT fk_admin_user_admin_role_role_id FOREIGN KEY (role_id) REFERENCES public.admin_role(id) ON DELETE SET NULL;


--
-- Name: amc_plan fk_amc_plan_category_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.amc_plan
    ADD CONSTRAINT fk_amc_plan_category_category_id FOREIGN KEY (category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: amc_service_visit fk_amc_service_visit_bookings_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.amc_service_visit
    ADD CONSTRAINT fk_amc_service_visit_bookings_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: amc_service_visit fk_amc_service_visit_customer_amc_contracts_contract_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.amc_service_visit
    ADD CONSTRAINT fk_amc_service_visit_customer_amc_contracts_contract_id FOREIGN KEY (contract_id) REFERENCES public.customer_amc_contract(id) ON DELETE RESTRICT;


--
-- Name: banner fk_banner_category_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.banner
    ADD CONSTRAINT fk_banner_category_category_id FOREIGN KEY (category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: banner fk_banner_cms_media_assets_media_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.banner
    ADD CONSTRAINT fk_banner_cms_media_assets_media_id FOREIGN KEY (media_id) REFERENCES public.cms_media(id) ON DELETE RESTRICT;


--
-- Name: booking_addon_item fk_booking_addon_item_booking_items_booking_item_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_addon_item
    ADD CONSTRAINT fk_booking_addon_item_booking_items_booking_item_id FOREIGN KEY (booking_item_id) REFERENCES public.booking_item(id) ON DELETE CASCADE;


--
-- Name: booking_cancellation fk_booking_cancellation_bookings_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_cancellation
    ADD CONSTRAINT fk_booking_cancellation_bookings_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: booking_completion_proof fk_booking_completion_proof_bookings_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_completion_proof
    ADD CONSTRAINT fk_booking_completion_proof_bookings_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE CASCADE;


--
-- Name: booking fk_booking_customer_amc_contracts_amc_contract_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT fk_booking_customer_amc_contracts_amc_contract_id FOREIGN KEY (amc_contract_id) REFERENCES public.customer_amc_contract(id) ON DELETE RESTRICT;


--
-- Name: booking fk_booking_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT fk_booking_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: booking_item fk_booking_item_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_item
    ADD CONSTRAINT fk_booking_item_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE CASCADE;


--
-- Name: booking_provider_assignment fk_booking_provider_assignment_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_provider_assignment
    ADD CONSTRAINT fk_booking_provider_assignment_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: booking_provider_assignment fk_booking_provider_assignment_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_provider_assignment
    ADD CONSTRAINT fk_booking_provider_assignment_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: booking fk_booking_providers_assigned_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT fk_booking_providers_assigned_provider_id FOREIGN KEY (assigned_provider_id) REFERENCES public.provider(id) ON DELETE SET NULL;


--
-- Name: booking fk_booking_recurring_booking_plans_recurring_booking_plan_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking
    ADD CONSTRAINT fk_booking_recurring_booking_plans_recurring_booking_plan_id FOREIGN KEY (recurring_booking_plan_id) REFERENCES public.recurring_booking_plan(id) ON DELETE RESTRICT;


--
-- Name: booking_reschedule fk_booking_reschedule_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_reschedule
    ADD CONSTRAINT fk_booking_reschedule_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: booking_status_history fk_booking_status_history_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.booking_status_history
    ADD CONSTRAINT fk_booking_status_history_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE CASCADE;


--
-- Name: category fk_category_category_parent_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.category
    ADD CONSTRAINT fk_category_category_parent_category_id FOREIGN KEY (parent_category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: category_city_mapping fk_category_city_mapping_category_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.category_city_mapping
    ADD CONSTRAINT fk_category_city_mapping_category_category_id FOREIGN KEY (category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: category_city_mapping fk_category_city_mapping_cities_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.category_city_mapping
    ADD CONSTRAINT fk_category_city_mapping_cities_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: chat_message fk_chat_message_chat_threads_thread_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.chat_message
    ADD CONSTRAINT fk_chat_message_chat_threads_thread_id FOREIGN KEY (thread_id) REFERENCES public.chat_thread(id) ON DELETE RESTRICT;


--
-- Name: city_pricing_policy fk_city_pricing_policy_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.city_pricing_policy
    ADD CONSTRAINT fk_city_pricing_policy_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: city fk_city_states_state_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.city
    ADD CONSTRAINT fk_city_states_state_id FOREIGN KEY (state_id) REFERENCES public.state(id) ON DELETE RESTRICT;


--
-- Name: coupon fk_coupon_category_applicable_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon
    ADD CONSTRAINT fk_coupon_category_applicable_category_id FOREIGN KEY (applicable_category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: coupon_customer_redemption_counter fk_coupon_customer_redemption_counter_coupon_coupon_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_customer_redemption_counter
    ADD CONSTRAINT fk_coupon_customer_redemption_counter_coupon_coupon_id FOREIGN KEY (coupon_id) REFERENCES public.coupon(id) ON DELETE CASCADE;


--
-- Name: coupon_customer_redemption_counter fk_coupon_customer_redemption_counter_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_customer_redemption_counter
    ADD CONSTRAINT fk_coupon_customer_redemption_counter_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE CASCADE;


--
-- Name: coupon_redemption fk_coupon_redemption_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_redemption
    ADD CONSTRAINT fk_coupon_redemption_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: coupon_redemption fk_coupon_redemption_coupon_coupon_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_redemption
    ADD CONSTRAINT fk_coupon_redemption_coupon_coupon_id FOREIGN KEY (coupon_id) REFERENCES public.coupon(id) ON DELETE RESTRICT;


--
-- Name: coupon_redemption fk_coupon_redemption_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.coupon_redemption
    ADD CONSTRAINT fk_coupon_redemption_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: customer_address fk_customer_address_localities_locality_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_address
    ADD CONSTRAINT fk_customer_address_localities_locality_id FOREIGN KEY (locality_id) REFERENCES public.locality(id) ON DELETE SET NULL;


--
-- Name: customer_address fk_customer_address_pincodes_pincode_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_address
    ADD CONSTRAINT fk_customer_address_pincodes_pincode_id FOREIGN KEY (pincode_id) REFERENCES public.pincode(id) ON DELETE SET NULL;


--
-- Name: customer_amc_contract fk_customer_amc_contract_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_amc_contract
    ADD CONSTRAINT fk_customer_amc_contract_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: customer_amc_contract fk_customer_amc_contract_payment_transactions_payment_transact; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_amc_contract
    ADD CONSTRAINT fk_customer_amc_contract_payment_transactions_payment_transact FOREIGN KEY (payment_transaction_id) REFERENCES public.payment_transaction(id) ON DELETE RESTRICT;


--
-- Name: customer_rating fk_customer_rating_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_rating
    ADD CONSTRAINT fk_customer_rating_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: customer_rating fk_customer_rating_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_rating
    ADD CONSTRAINT fk_customer_rating_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: customer_rating fk_customer_rating_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_rating
    ADD CONSTRAINT fk_customer_rating_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: customer_subscription fk_customer_subscription_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.customer_subscription
    ADD CONSTRAINT fk_customer_subscription_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: device_token fk_device_token_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.device_token
    ADD CONSTRAINT fk_device_token_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: device_token fk_device_token_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.device_token
    ADD CONSTRAINT fk_device_token_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: locality fk_locality_pincodes_pincode_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.locality
    ADD CONSTRAINT fk_locality_pincodes_pincode_id FOREIGN KEY (pincode_id) REFERENCES public.pincode(id) ON DELETE RESTRICT;


--
-- Name: locality fk_locality_zones_zone_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.locality
    ADD CONSTRAINT fk_locality_zones_zone_id FOREIGN KEY (zone_id) REFERENCES public.zone(id) ON DELETE RESTRICT;


--
-- Name: notification_event fk_notification_event_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_event
    ADD CONSTRAINT fk_notification_event_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: notification_event fk_notification_event_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_event
    ADD CONSTRAINT fk_notification_event_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: notification_event fk_notification_event_support_tickets_support_ticket_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.notification_event
    ADD CONSTRAINT fk_notification_event_support_tickets_support_ticket_id FOREIGN KEY (support_ticket_id) REFERENCES public.support_ticket(id) ON DELETE RESTRICT;


--
-- Name: payment_attempt fk_payment_attempt_payment_transactions_payment_transaction_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_attempt
    ADD CONSTRAINT fk_payment_attempt_payment_transactions_payment_transaction_id FOREIGN KEY (payment_transaction_id) REFERENCES public.payment_transaction(id) ON DELETE CASCADE;


--
-- Name: payment_transaction fk_payment_transaction_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_transaction
    ADD CONSTRAINT fk_payment_transaction_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: payment_transaction fk_payment_transaction_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.payment_transaction
    ADD CONSTRAINT fk_payment_transaction_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: pincode fk_pincode_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pincode
    ADD CONSTRAINT fk_pincode_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: platform_escrow_ledger fk_platform_escrow_ledger_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.platform_escrow_ledger
    ADD CONSTRAINT fk_platform_escrow_ledger_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: promotional_price fk_promotional_price_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotional_price
    ADD CONSTRAINT fk_promotional_price_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: promotional_price fk_promotional_price_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.promotional_price
    ADD CONSTRAINT fk_promotional_price_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: provider_availability_window fk_provider_availability_window_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_availability_window
    ADD CONSTRAINT fk_provider_availability_window_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_background_check fk_provider_background_check_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_background_check
    ADD CONSTRAINT fk_provider_background_check_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_blackout_date fk_provider_blackout_date_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_blackout_date
    ADD CONSTRAINT fk_provider_blackout_date_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_capacity fk_provider_capacity_providers_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_capacity
    ADD CONSTRAINT fk_provider_capacity_providers_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_earning_ledger fk_provider_earning_ledger_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_earning_ledger
    ADD CONSTRAINT fk_provider_earning_ledger_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_kyc_document fk_provider_kyc_document_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_kyc_document
    ADD CONSTRAINT fk_provider_kyc_document_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_payout fk_provider_payout_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_payout
    ADD CONSTRAINT fk_provider_payout_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_service_area fk_provider_service_area_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_service_area
    ADD CONSTRAINT fk_provider_service_area_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: provider_service_area fk_provider_service_area_pincodes_pincode_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_service_area
    ADD CONSTRAINT fk_provider_service_area_pincodes_pincode_id FOREIGN KEY (pincode_id) REFERENCES public.pincode(id) ON DELETE RESTRICT;


--
-- Name: provider_service_area fk_provider_service_area_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_service_area
    ADD CONSTRAINT fk_provider_service_area_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_service_area fk_provider_service_area_zones_zone_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_service_area
    ADD CONSTRAINT fk_provider_service_area_zones_zone_id FOREIGN KEY (zone_id) REFERENCES public.zone(id) ON DELETE RESTRICT;


--
-- Name: provider_skill_mapping fk_provider_skill_mapping_category_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_skill_mapping
    ADD CONSTRAINT fk_provider_skill_mapping_category_category_id FOREIGN KEY (category_id) REFERENCES public.category(id) ON DELETE RESTRICT;


--
-- Name: provider_skill_mapping fk_provider_skill_mapping_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_skill_mapping
    ADD CONSTRAINT fk_provider_skill_mapping_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: provider_skill_mapping fk_provider_skill_mapping_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.provider_skill_mapping
    ADD CONSTRAINT fk_provider_skill_mapping_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_occurrence fk_recurring_booking_occurrence_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_occurrence
    ADD CONSTRAINT fk_recurring_booking_occurrence_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_occurrence fk_recurring_booking_occurrence_recurring_booking_plans_recurr; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_occurrence
    ADD CONSTRAINT fk_recurring_booking_occurrence_recurring_booking_plans_recurr FOREIGN KEY (recurring_booking_plan_id) REFERENCES public.recurring_booking_plan(id) ON DELETE CASCADE;


--
-- Name: recurring_booking_plan_addon fk_recurring_booking_plan_addon_recurring_booking_plans_recurr; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan_addon
    ADD CONSTRAINT fk_recurring_booking_plan_addon_recurring_booking_plans_recurr FOREIGN KEY (recurring_booking_plan_id) REFERENCES public.recurring_booking_plan(id) ON DELETE CASCADE;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_customer_address_address_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_customer_address_address_id FOREIGN KEY (address_id) REFERENCES public.customer_address(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_locality_locality_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_locality_locality_id FOREIGN KEY (locality_id) REFERENCES public.locality(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: recurring_booking_plan fk_recurring_booking_plan_slot_windows_slot_window_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recurring_booking_plan
    ADD CONSTRAINT fk_recurring_booking_plan_slot_windows_slot_window_id FOREIGN KEY (slot_window_id) REFERENCES public.slot_window(id) ON DELETE RESTRICT;


--
-- Name: refund_transaction fk_refund_transaction_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.refund_transaction
    ADD CONSTRAINT fk_refund_transaction_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: refund_transaction fk_refund_transaction_payment_transaction_payment_transaction_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.refund_transaction
    ADD CONSTRAINT fk_refund_transaction_payment_transaction_payment_transaction_ FOREIGN KEY (payment_transaction_id) REFERENCES public.payment_transaction(id) ON DELETE RESTRICT;


--
-- Name: review fk_review_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.review
    ADD CONSTRAINT fk_review_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: review fk_review_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.review
    ADD CONSTRAINT fk_review_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: review fk_review_provider_provider_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.review
    ADD CONSTRAINT fk_review_provider_provider_id FOREIGN KEY (provider_id) REFERENCES public.provider(id) ON DELETE RESTRICT;


--
-- Name: review fk_review_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.review
    ADD CONSTRAINT fk_review_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: role_permission_mapping fk_role_permission_mapping_admin_permission_permission_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_permission_mapping
    ADD CONSTRAINT fk_role_permission_mapping_admin_permission_permission_id FOREIGN KEY (permission_id) REFERENCES public.admin_permission(id) ON DELETE RESTRICT;


--
-- Name: role_permission_mapping fk_role_permission_mapping_admin_role_role_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_permission_mapping
    ADD CONSTRAINT fk_role_permission_mapping_admin_role_role_id FOREIGN KEY (role_id) REFERENCES public.admin_role(id) ON DELETE RESTRICT;


--
-- Name: service_addon fk_service_addon_service_add_on_group_group_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_addon
    ADD CONSTRAINT fk_service_addon_service_add_on_group_group_id FOREIGN KEY (group_id) REFERENCES public.service_add_on_group(id) ON DELETE SET NULL;


--
-- Name: service_city_price fk_service_city_price_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_city_price
    ADD CONSTRAINT fk_service_city_price_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: service_city_price fk_service_city_price_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_city_price
    ADD CONSTRAINT fk_service_city_price_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: service_pincode_mapping fk_service_pincode_mapping_pincode_pincode_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_pincode_mapping
    ADD CONSTRAINT fk_service_pincode_mapping_pincode_pincode_id FOREIGN KEY (pincode_id) REFERENCES public.pincode(id) ON DELETE RESTRICT;


--
-- Name: service_pincode_mapping fk_service_pincode_mapping_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service_pincode_mapping
    ADD CONSTRAINT fk_service_pincode_mapping_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE RESTRICT;


--
-- Name: service fk_service_service_group_service_group_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.service
    ADD CONSTRAINT fk_service_service_group_service_group_id FOREIGN KEY (service_group_id) REFERENCES public.service_group(id) ON DELETE SET NULL;


--
-- Name: slot_availability_override fk_slot_availability_override_category_category_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_availability_override
    ADD CONSTRAINT fk_slot_availability_override_category_category_id FOREIGN KEY (category_id) REFERENCES public.category(id) ON DELETE SET NULL;


--
-- Name: slot_availability_override fk_slot_availability_override_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_availability_override
    ADD CONSTRAINT fk_slot_availability_override_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: slot_availability_override fk_slot_availability_override_service_service_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_availability_override
    ADD CONSTRAINT fk_slot_availability_override_service_service_id FOREIGN KEY (service_id) REFERENCES public.service(id) ON DELETE SET NULL;


--
-- Name: slot_availability_override fk_slot_availability_override_slot_windows_slot_window_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_availability_override
    ADD CONSTRAINT fk_slot_availability_override_slot_windows_slot_window_id FOREIGN KEY (slot_window_id) REFERENCES public.slot_window(id) ON DELETE SET NULL;


--
-- Name: slot_blackout fk_slot_blackout_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_blackout
    ADD CONSTRAINT fk_slot_blackout_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: slot_booking_counter fk_slot_booking_counter_slot_windows_slot_window_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_booking_counter
    ADD CONSTRAINT fk_slot_booking_counter_slot_windows_slot_window_id FOREIGN KEY (slot_window_id) REFERENCES public.slot_window(id) ON DELETE CASCADE;


--
-- Name: slot_booking_policy fk_slot_booking_policy_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_booking_policy
    ADD CONSTRAINT fk_slot_booking_policy_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: slot_window fk_slot_window_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_window
    ADD CONSTRAINT fk_slot_window_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- Name: slot_window_rule fk_slot_window_rule_slot_window_slot_window_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.slot_window_rule
    ADD CONSTRAINT fk_slot_window_rule_slot_window_slot_window_id FOREIGN KEY (slot_window_id) REFERENCES public.slot_window(id) ON DELETE CASCADE;


--
-- Name: support_ticket fk_support_ticket_admin_user_assigned_admin_user_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket
    ADD CONSTRAINT fk_support_ticket_admin_user_assigned_admin_user_id FOREIGN KEY (assigned_admin_user_id) REFERENCES public.admin_user(id) ON DELETE RESTRICT;


--
-- Name: support_ticket fk_support_ticket_booking_booking_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket
    ADD CONSTRAINT fk_support_ticket_booking_booking_id FOREIGN KEY (booking_id) REFERENCES public.booking(id) ON DELETE RESTRICT;


--
-- Name: support_ticket_comment fk_support_ticket_comment_support_tickets_support_ticket_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket_comment
    ADD CONSTRAINT fk_support_ticket_comment_support_tickets_support_ticket_id FOREIGN KEY (support_ticket_id) REFERENCES public.support_ticket(id) ON DELETE CASCADE;


--
-- Name: support_ticket fk_support_ticket_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.support_ticket
    ADD CONSTRAINT fk_support_ticket_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: wallet_ledger fk_wallet_ledger_customer_customer_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.wallet_ledger
    ADD CONSTRAINT fk_wallet_ledger_customer_customer_id FOREIGN KEY (customer_id) REFERENCES public.customer(id) ON DELETE RESTRICT;


--
-- Name: zone fk_zone_city_city_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.zone
    ADD CONSTRAINT fk_zone_city_city_id FOREIGN KEY (city_id) REFERENCES public.city(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict aU35IRHRsbnfzsjmlTkpO3DQkYSnXktPtPzO2K4TTxCjypYIwYLdO9ncGR5MJsP

