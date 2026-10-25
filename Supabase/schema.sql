-- LestingCraft: New Era — economy, marketplace, friends and cosmetics.
-- Idempotent: safe to re-run. Apply with `npm run db:apply` from Supabase/.
--
-- Security model
--   * Every table has RLS on and no direct write policies: mutations go through
--     SECURITY DEFINER functions that validate the caller.
--   * A caller may act on a profile when it is their own (auth.uid() matched to
--     profiles.auth_user_id) or when it is the service role (game Worker).
--   * Coins are never granted at signup. The only sources are paid top-ups
--     credited by the payment webhook and player-to-player marketplace sales.
--   * Balances are guarded by a CHECK (balance >= 0); every change writes a
--     ledger row, so the economy is auditable.

create extension if not exists pgcrypto;

-- ---------------------------------------------------------------------------
-- Profiles: the bridge between the Cloudflare player identity (32 hex
-- player_key, already used by the game server) and an optional web account.
-- ---------------------------------------------------------------------------
create table if not exists public.profiles (
  id            uuid primary key default gen_random_uuid(),
  player_key    text unique check (player_key ~ '^[a-f0-9]{32}$'),
  auth_user_id  uuid unique references auth.users(id) on delete cascade,
  display_name  text not null check (char_length(display_name) between 1 and 24),
  avatar_skin   text,
  created_at    timestamptz not null default now(),
  last_seen_at  timestamptz not null default now()
);

comment on table public.profiles is
  'One row per player. player_key is the in-game identity, auth_user_id is the optional web store login.';

-- ---------------------------------------------------------------------------
-- Wallet and ledger
-- ---------------------------------------------------------------------------
create table if not exists public.wallets (
  profile_id uuid primary key references public.profiles(id) on delete cascade,
  balance    bigint not null default 0 check (balance >= 0),
  updated_at timestamptz not null default now()
);

create table if not exists public.wallet_ledger (
  id         bigserial primary key,
  profile_id uuid not null references public.profiles(id) on delete cascade,
  delta      bigint not null,
  reason     text not null check (reason in ('topup','purchase','market_sale','market_purchase','refund','admin')),
  reference  text,
  balance_after bigint not null,
  created_at timestamptz not null default now(),
  unique (profile_id, reference) -- idempotency for webhook-driven credits
);

create index if not exists wallet_ledger_profile_idx on public.wallet_ledger (profile_id, created_at desc);

-- ---------------------------------------------------------------------------
-- Real-money packs. Prices live server-side; the client never sends an amount.
-- No starter pack is auto-granted: a new wallet is exactly 0.
-- ---------------------------------------------------------------------------
create table if not exists public.currency_packs (
  code         text primary key,
  coins        bigint not null check (coins > 0),
  bonus_coins  bigint not null default 0 check (bonus_coins >= 0),
  price_minor  integer not null check (price_minor > 0),
  currency     char(3) not null default 'USD',
  title        text not null,
  active       boolean not null default false, -- enabled once a payment provider is connected
  sort_order   integer not null default 0
);

create table if not exists public.topups (
  id            uuid primary key default gen_random_uuid(),
  profile_id    uuid not null references public.profiles(id) on delete cascade,
  pack_code     text not null references public.currency_packs(code),
  provider      text not null,
  provider_ref  text not null,
  amount_minor  integer not null,
  currency      char(3) not null,
  coins         bigint not null,
  status        text not null default 'pending' check (status in ('pending','paid','failed','refunded')),
  created_at    timestamptz not null default now(),
  unique (provider, provider_ref)
);

-- ---------------------------------------------------------------------------
-- Cosmetics (skins, capes, emotes) sold for coins.
-- ---------------------------------------------------------------------------
create table if not exists public.store_items (
  id            uuid primary key default gen_random_uuid(),
  code          text unique not null,
  kind          text not null check (kind in ('skin','cape','emote','bundle')),
  title         text not null,
  description   text,
  price_coins   bigint not null check (price_coins >= 0),
  payload       jsonb not null default '{}'::jsonb, -- palette, texture path, model overrides
  preview_url   text,
  active        boolean not null default true,
  created_at    timestamptz not null default now()
);

create table if not exists public.entitlements (
  profile_id  uuid not null references public.profiles(id) on delete cascade,
  item_id     uuid not null references public.store_items(id) on delete cascade,
  source      text not null default 'purchase' check (source in ('purchase','reward','gift','market')),
  acquired_at timestamptz not null default now(),
  primary key (profile_id, item_id)
);

-- ---------------------------------------------------------------------------
-- Player-to-player marketplace. A listing escrows the entitlement.
-- ---------------------------------------------------------------------------
create table if not exists public.market_listings (
  id          uuid primary key default gen_random_uuid(),
  seller_id   uuid not null references public.profiles(id) on delete cascade,
  item_id     uuid not null references public.store_items(id) on delete cascade,
  price_coins bigint not null check (price_coins > 0),
  status      text not null default 'active' check (status in ('active','sold','cancelled','expired')),
  buyer_id    uuid references public.profiles(id),
  created_at  timestamptz not null default now(),
  closed_at   timestamptz
);

create unique index if not exists market_one_active_listing_per_item
  on public.market_listings (item_id) where status = 'active';
create index if not exists market_active_idx on public.market_listings (status, created_at desc);

create table if not exists public.market_orders (
  id          bigserial primary key,
  listing_id  uuid not null references public.market_listings(id) on delete cascade,
  buyer_id    uuid not null references public.profiles(id),
  seller_id   uuid not null references public.profiles(id),
  price_coins bigint not null,
  fee_coins   bigint not null default 0,
  created_at  timestamptz not null default now()
);

-- ---------------------------------------------------------------------------
-- Friends
-- ---------------------------------------------------------------------------
create table if not exists public.friend_requests (
  id         bigserial primary key,
  from_id    uuid not null references public.profiles(id) on delete cascade,
  to_id      uuid not null references public.profiles(id) on delete cascade,
  message    text check (char_length(message) <= 140),
  created_at timestamptz not null default now(),
  check (from_id <> to_id),
  unique (from_id, to_id)
);

create table if not exists public.friendships (
  low_id     uuid not null references public.profiles(id) on delete cascade,
  high_id    uuid not null references public.profiles(id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (low_id, high_id),
  check (low_id < high_id)
);

create table if not exists public.player_blocks (
  blocker_id uuid not null references public.profiles(id) on delete cascade,
  blocked_id uuid not null references public.profiles(id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (blocker_id, blocked_id),
  check (blocker_id <> blocked_id)
);

-- ---------------------------------------------------------------------------
-- One-time codes that let a logged-in web account claim an in-game identity.
-- ---------------------------------------------------------------------------
create table if not exists public.player_link_codes (
  code       text primary key check (code ~ '^[A-Z0-9]{8}$'),
  profile_id uuid not null references public.profiles(id) on delete cascade,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default now() + interval '10 minutes',
  used_at    timestamptz
);

-- ---------------------------------------------------------------------------
-- RLS: read your own rows, never write directly.
-- ---------------------------------------------------------------------------
alter table public.profiles          enable row level security;
alter table public.wallets           enable row level security;
alter table public.wallet_ledger     enable row level security;
alter table public.currency_packs    enable row level security;
alter table public.topups            enable row level security;
alter table public.store_items       enable row level security;
alter table public.entitlements      enable row level security;
alter table public.market_listings   enable row level security;
alter table public.market_orders     enable row level security;
alter table public.friend_requests   enable row level security;
alter table public.friendships       enable row level security;
alter table public.player_blocks     enable row level security;
alter table public.player_link_codes enable row level security;

-- Helper: is the current caller this profile (or the service role)?
create or replace function public.owns_profile(p_profile uuid)
returns boolean
language sql stable security definer set search_path = public, pg_temp as $$
  select (select auth.role()) = 'service_role'
      or exists (select 1 from public.profiles p
                 where p.id = p_profile and p.auth_user_id = (select auth.uid()));
$$;

drop policy if exists profiles_read_self on public.profiles;
create policy profiles_read_self on public.profiles
  for select using (owns_profile(id));

drop policy if exists wallets_read_self on public.wallets;
create policy wallets_read_self on public.wallets
  for select using (owns_profile(profile_id));

drop policy if exists ledger_read_self on public.wallet_ledger;
create policy ledger_read_self on public.wallet_ledger
  for select using (owns_profile(profile_id));

drop policy if exists entitlements_read_self on public.entitlements;
create policy entitlements_read_self on public.entitlements
  for select using (owns_profile(profile_id));

-- The public catalogue is readable by anyone, including signed-out visitors.
drop policy if exists store_items_public on public.store_items;
create policy store_items_public on public.store_items
  for select using (active or (select auth.role()) = 'service_role');

drop policy if exists packs_public on public.currency_packs;
create policy packs_public on public.currency_packs
  for select using (active or (select auth.role()) = 'service_role');

drop policy if exists market_read_active on public.market_listings;
create policy market_read_active on public.market_listings
  for select using (status = 'active' or owns_profile(seller_id) or owns_profile(buyer_id));

drop policy if exists market_orders_read on public.market_orders;
create policy market_orders_read on public.market_orders
  for select using (owns_profile(buyer_id) or owns_profile(seller_id));

drop policy if exists requests_read_own on public.friend_requests;
create policy requests_read_own on public.friend_requests
  for select using (owns_profile(from_id) or owns_profile(to_id));

drop policy if exists friendships_read_own on public.friendships;
create policy friendships_read_own on public.friendships
  for select using (owns_profile(low_id) or owns_profile(high_id));

drop policy if exists topups_read_own on public.topups;
create policy topups_read_own on public.topups
  for select using (owns_profile(profile_id));

drop policy if exists blocks_read_own on public.player_blocks;
create policy blocks_read_own on public.player_blocks
  for select using (owns_profile(blocker_id));

-- ---------------------------------------------------------------------------
-- Internal helpers
-- ---------------------------------------------------------------------------
create or replace function public.assert_profile_access(p_profile uuid)
returns void
language plpgsql stable security definer set search_path = public, pg_temp as $$
begin
  if not public.owns_profile(p_profile) then
    raise exception 'not allowed for this profile' using errcode = '42501';
  end if;
end $$;

create or replace function public.wallet_post(
  p_profile uuid, p_delta bigint, p_reason text, p_reference text default null)
returns bigint
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_balance bigint;
begin
  insert into public.wallets (profile_id) values (p_profile)
    on conflict (profile_id) do nothing;

  update public.wallets w
     set balance = w.balance + p_delta, updated_at = now()
   where w.profile_id = p_profile
     and w.balance + p_delta >= 0
  returning w.balance into v_balance;

  if v_balance is null then
    raise exception 'insufficient coins' using errcode = '22000';
  end if;

  insert into public.wallet_ledger (profile_id, delta, reason, reference, balance_after)
  values (p_profile, p_delta, p_reason, p_reference, v_balance)
  on conflict (profile_id, reference) do nothing;

  return v_balance;
end $$;

-- ---------------------------------------------------------------------------
-- Public RPCs
-- ---------------------------------------------------------------------------
create or replace function public.my_profile()
returns table (id uuid, display_name text, avatar_skin text, balance bigint)
language sql stable security definer set search_path = public, pg_temp as $$
  select p.id, p.display_name, p.avatar_skin, coalesce(w.balance, 0)
    from public.profiles p
    left join public.wallets w on w.profile_id = p.id
   where p.auth_user_id = (select auth.uid());
$$;

-- Top-ups are credited only by the payment webhook (service role), never by a client.
create or replace function public.svc_topup_credit(
  p_provider_ref text, p_profile uuid, p_pack text, p_provider text,
  p_amount_minor integer, p_currency char(3))
returns bigint
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_pack public.currency_packs; v_coins bigint;
begin
  if (select auth.role()) <> 'service_role' then
    raise exception 'service role required' using errcode = '42501';
  end if;

  select * into v_pack from public.currency_packs where code = p_pack and active;
  if not found then raise exception 'unknown or inactive pack %', p_pack; end if;
  if p_amount_minor <> v_pack.price_minor or p_currency <> v_pack.currency then
    raise exception 'amount mismatch for pack %', p_pack;
  end if;

  insert into public.topups (profile_id, pack_code, provider, provider_ref, amount_minor, currency, coins, status)
  values (p_profile, p_pack, p_provider, p_provider_ref, p_amount_minor, p_currency,
          v_pack.coins + v_pack.bonus_coins, 'paid')
  on conflict (provider, provider_ref) do nothing;

  if not found then return null; end if; -- replay: already credited

  v_coins := v_pack.coins + v_pack.bonus_coins;
  return public.wallet_post(p_profile, v_coins, 'topup', p_provider || ':' || p_provider_ref);
end $$;

create or replace function public.store_buy(p_item_code text)
returns table (item_id uuid, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid; v_item public.store_items; v_balance bigint;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile for this account' using errcode = '42501'; end if;

  select * into v_item from public.store_items where code = p_item_code and active;
  if not found then raise exception 'unknown item %', p_item_code; end if;

  if exists (select 1 from public.entitlements e
              where e.profile_id = v_profile and e.item_id = v_item.id) then
    raise exception 'already owned';
  end if;

  v_balance := public.wallet_post(v_profile, -v_item.price_coins, 'purchase', 'item:' || v_item.code);

  insert into public.entitlements (profile_id, item_id, source) values (v_profile, v_item.id, 'purchase');

  return query select v_item.id, v_balance;
end $$;

create or replace function public.equip_skin(p_item_code text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile for this account' using errcode = '42501'; end if;

  if not exists (select 1 from public.entitlements e join public.store_items i on i.id = e.item_id
                  where e.profile_id = v_profile and i.code = p_item_code and i.kind in ('skin','cape')) then
    raise exception 'skin not owned';
  end if;

  update public.profiles set avatar_skin = p_item_code where id = v_profile;
  return p_item_code;
end $$;

create or replace function public.market_list(p_item_code text, p_price bigint)
returns uuid
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid; v_item uuid; v_listing uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile' using errcode = '42501'; end if;
  if p_price <= 0 then raise exception 'price must be positive'; end if;

  select i.id into v_item from public.store_items i where i.code = p_item_code;
  if v_item is null then raise exception 'unknown item';
  elsif not exists (select 1 from public.entitlements e where e.profile_id = v_profile and e.item_id = v_item) then
    raise exception 'item not owned';
  end if;

  -- The item leaves the seller's inventory while the listing is active (escrow).
  delete from public.entitlements where profile_id = v_profile and item_id = v_item;

  insert into public.market_listings (seller_id, item_id, price_coins)
  values (v_profile, v_item, p_price) returning id into v_listing;

  return v_listing;
exception when unique_violation then
  raise exception 'this item is already listed';
end $$;

create or replace function public.market_cancel(p_listing uuid)
returns void
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_listing public.market_listings;
begin
  select * into v_listing from public.market_listings where id = p_listing for update;
  if not found then raise exception 'listing not found'; end if;
  perform public.assert_profile_access(v_listing.seller_id);
  if v_listing.status <> 'active' then raise exception 'listing is not active'; end if;

  update public.market_listings set status = 'cancelled', closed_at = now() where id = p_listing;
  insert into public.entitlements (profile_id, item_id, source) values (v_listing.seller_id, v_listing.item_id, 'market')
    on conflict do nothing;
end $$;

create or replace function public.market_buy(p_listing uuid)
returns table (item_code text, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_listing public.market_listings; v_profile uuid; v_balance bigint;
        v_fee bigint; v_fee_bps constant integer := 500; -- 5% marketplace fee
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile' using errcode = '42501'; end if;

  select * into v_listing from public.market_listings where id = p_listing for update;
  if not found or v_listing.status <> 'active' then raise exception 'listing is not active';
  elsif v_listing.seller_id = v_profile then raise exception 'cannot buy your own listing';
  elsif exists (select 1 from public.player_blocks b
                 where (b.blocker_id = v_profile and b.blocked_id = v_listing.seller_id)
                    or (b.blocker_id = v_listing.seller_id and b.blocked_id = v_profile)) then
    raise exception 'trade blocked';
  end if;

  v_fee := floor(v_listing.price_coins * v_fee_bps / 10000);

  v_balance := public.wallet_post(v_profile, -v_listing.price_coins, 'market_purchase', 'listing:' || p_listing);
  perform public.wallet_post(v_listing.seller_id, v_listing.price_coins - v_fee, 'market_sale', 'listing:' || p_listing);

  update public.market_listings
     set status = 'sold', buyer_id = v_profile, closed_at = now()
   where id = p_listing;

  insert into public.market_orders (listing_id, buyer_id, seller_id, price_coins, fee_coins)
  values (p_listing, v_profile, v_listing.seller_id, v_listing.price_coins, v_fee);

  insert into public.entitlements (profile_id, item_id, source)
  values (v_profile, v_listing.item_id, 'market')
  on conflict do nothing;

  return query select i.code, v_balance from public.store_items i where i.id = v_listing.item_id;
end $$;

-- Friends ------------------------------------------------------------------
create or replace function public.friend_request(p_player_key text, p_message text default null)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid; v_target uuid;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;

  select p.id into v_target from public.profiles p where p.player_key = lower(p_player_key);

  -- Always answer the same way: an unknown key must not be distinguishable
  -- from a known one, otherwise the endpoint becomes a player-key oracle.
  if v_target is null or v_target = v_me then return 'sent'; end if;
  if exists (select 1 from public.player_blocks b
              where (b.blocker_id = v_target and b.blocked_id = v_me)) then return 'sent'; end if;

  if exists (select 1 from public.friendships f
              where f.low_id = least(v_me, v_target) and f.high_id = greatest(v_me, v_target)) then
    return 'already_friends';
  end if;

  insert into public.friend_requests (from_id, to_id, message)
  values (v_me, v_target, left(coalesce(p_message, ''), 140))
  on conflict (from_id, to_id) do update set created_at = now();

  return 'sent';
end $$;

create or replace function public.friend_accept(p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid; v_req public.friend_requests;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;

  select * into v_req from public.friend_requests where id = p_request and to_id = v_me;
  if not found then raise exception 'request not found'; end if;

  insert into public.friendships (low_id, high_id)
  values (least(v_req.from_id, v_me), greatest(v_req.from_id, v_me))
  on conflict do nothing;

  delete from public.friend_requests where id = p_request;
  return 'accepted';
end $$;

create or replace function public.friend_decline(p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
begin
  delete from public.friend_requests
   where id = p_request
     and to_id = (select p.id from public.profiles p where p.auth_user_id = (select auth.uid()));
  return 'declined';
end $$;

create or replace function public.friend_remove(p_player_key text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid; v_other uuid;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;

  select p.id into v_other from public.profiles p where p.player_key = lower(p_player_key);
  if v_other is null then return 'removed'; end if;

  delete from public.friendships
   where low_id = least(v_me, v_other) and high_id = greatest(v_me, v_other);
  delete from public.friend_requests
   where (from_id = v_me and to_id = v_other) or (from_id = v_other and to_id = v_me);
  return 'removed';
end $$;

create or replace function public.friend_list()
returns table (profile_id uuid, display_name text, player_key text, avatar_skin text, online boolean)
language sql stable security definer set search_path = public, pg_temp as $$
  with me as (select p.id from public.profiles p where p.auth_user_id = (select auth.uid()))
  select p.id, p.display_name, p.player_key, p.avatar_skin,
         p.last_seen_at > now() - interval '5 minutes'
    from public.friendships f
    join me on true
    join public.profiles p
      on p.id = case when f.low_id = me.id then f.high_id else f.low_id end
   -- Without this filter the join returned one side of EVERY friendship in the
   -- database, so a player could read other people's friend lists.
   where f.low_id = me.id or f.high_id = me.id;
$$;

-- ---------------------------------------------------------------------------
-- Grants: catalogue is public, everything else needs a logged-in user.
-- ---------------------------------------------------------------------------
revoke all on function public.svc_topup_credit(text, uuid, text, text, integer, char) from public, anon, authenticated;
grant execute on function public.svc_topup_credit(text, uuid, text, text, integer, char) to service_role;

grant execute on function public.my_profile() to authenticated, service_role;
grant execute on function public.owns_profile(uuid) to authenticated, service_role;
grant execute on function public.store_buy(text) to authenticated, service_role;
grant execute on function public.equip_skin(text) to authenticated, service_role;
grant execute on function public.market_list(text, bigint) to authenticated, service_role;
grant execute on function public.market_cancel(uuid) to authenticated, service_role;
grant execute on function public.market_buy(uuid) to authenticated, service_role;
grant execute on function public.friend_request(text, text) to authenticated, service_role;
grant execute on function public.friend_accept(bigint) to authenticated, service_role;
grant execute on function public.friend_decline(bigint) to authenticated, service_role;
grant execute on function public.friend_remove(text) to authenticated, service_role;
grant execute on function public.friend_list() to authenticated, service_role;

-- Seed catalogue -----------------------------------------------------------
insert into public.currency_packs (code, coins, bonus_coins, price_minor, currency, title, active, sort_order)
values
  ('pouch',   500,   0,  199, 'USD', 'Кошель',        false, 1),
  ('satchel', 1200, 100, 499, 'USD', 'Сумка',         false, 2),
  ('chest',   2600, 400, 999, 'USD', 'Сундук',        false, 3),
  ('vault',   5600, 900, 1999,'USD', 'Хранилище',     false, 4)
on conflict (code) do nothing;

insert into public.store_items (code, kind, title, description, price_coins, payload)
values
  ('skin_ember',   'skin', 'Уголёк',        'Тёплая палитра с янтарной подсветкой.', 450,  '{"palette":["#efba69","#8c3b1e","#2a1710"]}'),
  ('skin_tide',    'skin', 'Прилив',        'Холодные лазурные тона.',               450,  '{"palette":["#7fd4e8","#1e5c73","#0b1c26"]}'),
  ('skin_verdant', 'skin', 'Зелень',        'Мшистый зелёный с медью.',              350,  '{"palette":["#8cc66a","#3d5f34","#101a12"]}'),
  ('cape_aurora',  'cape', 'Аврора',        'Плащ с переливом северного сияния.',    900,  '{"gradient":["#7ff2c8","#5b8def","#a56bff"]}'),
  ('cape_ash',     'cape', 'Пепел',         'Тёмный плащ с тлеющими искрами.',       700,  '{"gradient":["#3a3a3a","#d9644a"]}')
on conflict (code) do nothing;
