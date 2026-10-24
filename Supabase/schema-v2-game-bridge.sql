-- LestingCraft: New Era — game bridge (v2).
--
-- The game server is the only holder of the service_role key. A player is
-- identified there by player_key (32 hex) — for logged-in accounts that is the
-- account uid, which the accounts service already issues as 32 hex chars.
--
-- This file adds:
--   * _impl functions that take an explicit profile id (one source of truth),
--   * the original user-facing RPCs re-expressed on top of those impls,
--   * svc_* RPCs for the game server: same behaviour, keyed by player_key and
--     executable by the service role only.
--
-- Idempotent: safe to re-run. Apply with `npm run db:bridge`.

-- ---------------------------------------------------------------------------
-- Guard: every svc_* entry point starts here.
-- ---------------------------------------------------------------------------
create or replace function public.assert_service_role()
returns void
language plpgsql stable security definer set search_path = public, pg_temp as $$
begin
  if coalesce((select auth.role()), '') <> 'service_role' then
    raise exception 'service role required' using errcode = '42501';
  end if;
end $$;

-- Resolve a player_key to a profile, creating it on first contact.
-- Coins are never granted here: a fresh wallet is exactly 0.
create or replace function public.svc_ensure_profile(p_player_key text, p_display_name text default null)
returns uuid
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  if p_player_key is null or p_player_key !~ '^[a-f0-9]{32}$' then
    raise exception 'invalid player key';
  end if;

  insert into public.profiles (player_key, display_name)
  values (p_player_key, left(coalesce(nullif(btrim(p_display_name), ''), 'Игрок'), 24))
  on conflict (player_key) do nothing;

  select id into v_id from public.profiles where player_key = p_player_key;
  insert into public.wallets (profile_id) values (v_id) on conflict (profile_id) do nothing;
  update public.profiles set last_seen_at = now() where id = v_id;
  return v_id;
end $$;

create or replace function public.svc_profile_id(p_player_key text)
returns uuid
language plpgsql stable security definer set search_path = public, pg_temp as $$
begin
  perform public.assert_service_role();
  return (select p.id from public.profiles p where p.player_key = lower(p_player_key));
end $$;

-- ---------------------------------------------------------------------------
-- Store: buy and equip, with the calling profile made explicit.
-- ---------------------------------------------------------------------------
create or replace function public._store_buy(p_profile uuid, p_item_code text)
returns table (item_id uuid, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_item public.store_items; v_balance bigint;
begin
  select * into v_item from public.store_items where code = p_item_code and active;
  if not found then raise exception 'unknown item %', p_item_code; end if;

  if exists (select 1 from public.entitlements e
              where e.profile_id = p_profile and e.item_id = v_item.id) then
    raise exception 'already owned';
  end if;

  v_balance := public.wallet_post(p_profile, -v_item.price_coins, 'purchase', 'item:' || v_item.code);
  insert into public.entitlements (profile_id, item_id, source) values (p_profile, v_item.id, 'purchase');
  return query select v_item.id, v_balance;
end $$;

create or replace function public._equip_skin(p_profile uuid, p_item_code text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
begin
  if not exists (select 1 from public.entitlements e join public.store_items i on i.id = e.item_id
                  where e.profile_id = p_profile and i.code = p_item_code and i.kind in ('skin','cape')) then
    raise exception 'skin not owned';
  end if;
  update public.profiles set avatar_skin = p_item_code where id = p_profile;
  return p_item_code;
end $$;

create or replace function public.store_buy(p_item_code text)
returns table (item_id uuid, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile for this account' using errcode = '42501'; end if;
  return query select * from public._store_buy(v_profile, p_item_code);
end $$;

create or replace function public.equip_skin(p_item_code text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile for this account' using errcode = '42501'; end if;
  return public._equip_skin(v_profile, p_item_code);
end $$;

-- ---------------------------------------------------------------------------
-- Marketplace: escrow on listing, 5% fee paid by the seller.
-- ---------------------------------------------------------------------------
create or replace function public._market_list(p_profile uuid, p_item_code text, p_price bigint)
returns uuid
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_item uuid; v_listing uuid;
begin
  if p_price is null or p_price <= 0 then raise exception 'price must be positive'; end if;

  select i.id into v_item from public.store_items i where i.code = p_item_code;
  if v_item is null then raise exception 'unknown item';
  elsif not exists (select 1 from public.entitlements e where e.profile_id = p_profile and e.item_id = v_item) then
    raise exception 'item not owned';
  end if;

  delete from public.entitlements where profile_id = p_profile and item_id = v_item;

  insert into public.market_listings (seller_id, item_id, price_coins)
  values (p_profile, v_item, p_price) returning id into v_listing;
  return v_listing;
exception when unique_violation then
  raise exception 'this item is already listed';
end $$;

create or replace function public._market_cancel(p_profile uuid, p_listing uuid)
returns void
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_listing public.market_listings;
begin
  select * into v_listing from public.market_listings where id = p_listing for update;
  if not found then raise exception 'listing not found'; end if;
  if v_listing.seller_id <> p_profile then
    raise exception 'not allowed for this profile' using errcode = '42501';
  end if;
  if v_listing.status <> 'active' then raise exception 'listing is not active'; end if;

  update public.market_listings set status = 'cancelled', closed_at = now() where id = p_listing;
  insert into public.entitlements (profile_id, item_id, source)
  values (v_listing.seller_id, v_listing.item_id, 'market') on conflict do nothing;
end $$;

create or replace function public._market_buy(p_profile uuid, p_listing uuid)
returns table (item_code text, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_listing public.market_listings; v_balance bigint; v_fee bigint;
        v_fee_bps constant integer := 500;
begin
  select * into v_listing from public.market_listings where id = p_listing for update;
  if not found or v_listing.status <> 'active' then raise exception 'listing is not active';
  elsif v_listing.seller_id = p_profile then raise exception 'cannot buy your own listing';
  elsif exists (select 1 from public.player_blocks b
                 where (b.blocker_id = p_profile and b.blocked_id = v_listing.seller_id)
                    or (b.blocker_id = v_listing.seller_id and b.blocked_id = p_profile)) then
    raise exception 'trade blocked';
  end if;

  v_fee := floor(v_listing.price_coins * v_fee_bps / 10000);
  v_balance := public.wallet_post(p_profile, -v_listing.price_coins, 'market_purchase', 'listing:' || p_listing);
  perform public.wallet_post(v_listing.seller_id, v_listing.price_coins - v_fee, 'market_sale', 'listing:' || p_listing);

  update public.market_listings
     set status = 'sold', buyer_id = p_profile, closed_at = now()
   where id = p_listing;

  insert into public.market_orders (listing_id, buyer_id, seller_id, price_coins, fee_coins)
  values (p_listing, p_profile, v_listing.seller_id, v_listing.price_coins, v_fee);

  insert into public.entitlements (profile_id, item_id, source)
  values (p_profile, v_listing.item_id, 'market') on conflict do nothing;

  return query select i.code, v_balance from public.store_items i where i.id = v_listing.item_id;
end $$;

create or replace function public.market_list(p_item_code text, p_price bigint)
returns uuid
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile' using errcode = '42501'; end if;
  return public._market_list(v_profile, p_item_code, p_price);
end $$;

create or replace function public.market_cancel(p_listing uuid)
returns void
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile' using errcode = '42501'; end if;
  perform public._market_cancel(v_profile, p_listing);
end $$;

create or replace function public.market_buy(p_listing uuid)
returns table (item_code text, balance bigint)
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  select p.id into v_profile from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_profile is null then raise exception 'no profile' using errcode = '42501'; end if;
  return query select * from public._market_buy(v_profile, p_listing);
end $$;

-- ---------------------------------------------------------------------------
-- Friends: same rules, same "unknown key looks like a success" behaviour.
-- ---------------------------------------------------------------------------
create or replace function public._friend_request(p_me uuid, p_player_key text, p_message text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_target uuid;
begin
  select p.id into v_target from public.profiles p where p.player_key = lower(p_player_key);
  if v_target is null or v_target = p_me then return 'sent'; end if;
  if exists (select 1 from public.player_blocks b
              where (b.blocker_id = v_target and b.blocked_id = p_me)) then return 'sent'; end if;
  if exists (select 1 from public.friendships f
              where f.low_id = least(p_me, v_target) and f.high_id = greatest(p_me, v_target)) then
    return 'already_friends';
  end if;

  insert into public.friend_requests (from_id, to_id, message)
  values (p_me, v_target, left(coalesce(p_message, ''), 140))
  on conflict (from_id, to_id) do update set created_at = now();
  return 'sent';
end $$;

create or replace function public._friend_accept(p_me uuid, p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_req public.friend_requests;
begin
  select * into v_req from public.friend_requests where id = p_request and to_id = p_me;
  if not found then raise exception 'request not found'; end if;

  insert into public.friendships (low_id, high_id)
  values (least(v_req.from_id, p_me), greatest(v_req.from_id, p_me))
  on conflict do nothing;

  delete from public.friend_requests where id = p_request;
  return 'accepted';
end $$;

create or replace function public._friend_decline(p_me uuid, p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
begin
  delete from public.friend_requests where id = p_request and to_id = p_me;
  return 'declined';
end $$;

create or replace function public._friend_remove(p_me uuid, p_player_key text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_other uuid;
begin
  select p.id into v_other from public.profiles p where p.player_key = lower(p_player_key);
  if v_other is null then return 'removed'; end if;

  delete from public.friendships
   where low_id = least(p_me, v_other) and high_id = greatest(p_me, v_other);
  delete from public.friend_requests
   where (from_id = p_me and to_id = v_other) or (from_id = v_other and to_id = p_me);
  return 'removed';
end $$;

create or replace function public._friend_list(p_me uuid)
returns table (profile_id uuid, display_name text, player_key text, avatar_skin text, online boolean)
language sql stable security definer set search_path = public, pg_temp as $$
  select p.id, p.display_name, p.player_key, p.avatar_skin,
         p.last_seen_at > now() - interval '5 minutes'
    from public.friendships f
    join public.profiles p
      on p.id = case when f.low_id = p_me then f.high_id else f.low_id end
   where f.low_id = p_me or f.high_id = p_me;
$$;

create or replace function public.friend_request(p_player_key text, p_message text default null)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;
  return public._friend_request(v_me, p_player_key, p_message);
end $$;

create or replace function public.friend_accept(p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;
  return public._friend_accept(v_me, p_request);
end $$;

create or replace function public.friend_decline(p_request bigint)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
begin
  return public._friend_decline(
    (select p.id from public.profiles p where p.auth_user_id = (select auth.uid())), p_request);
end $$;

create or replace function public.friend_remove(p_player_key text)
returns text
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_me uuid;
begin
  select p.id into v_me from public.profiles p where p.auth_user_id = (select auth.uid());
  if v_me is null then raise exception 'no profile' using errcode = '42501'; end if;
  return public._friend_remove(v_me, p_player_key);
end $$;

create or replace function public.friend_list()
returns table (profile_id uuid, display_name text, player_key text, avatar_skin text, online boolean)
language sql stable security definer set search_path = public, pg_temp as $$
  select * from public._friend_list((select p.id from public.profiles p where p.auth_user_id = (select auth.uid())));
$$;

-- ---------------------------------------------------------------------------
-- Game server entry points. Everything below is service_role only.
-- ---------------------------------------------------------------------------
create or replace function public.svc_state(p_player_key text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);

  return jsonb_build_object(
    'profile', (select jsonb_build_object('id', p.id, 'display_name', p.display_name,
                                          'avatar_skin', p.avatar_skin, 'player_key', p.player_key)
                  from public.profiles p where p.id = v_id),
    'balance', (select coalesce(w.balance, 0) from public.wallets w where w.profile_id = v_id),
    'owned', coalesce((select jsonb_agg(jsonb_build_object('code', i.code, 'kind', i.kind,
                                                           'title', i.title, 'payload', i.payload)
                                        order by i.kind, i.code)
                         from public.entitlements e join public.store_items i on i.id = e.item_id
                        where e.profile_id = v_id), '[]'::jsonb),
    'store', coalesce((select jsonb_agg(jsonb_build_object('code', i.code, 'kind', i.kind, 'title', i.title,
                                                           'description', i.description, 'price', i.price_coins,
                                                           'payload', i.payload)
                                        order by i.kind, i.price_coins, i.code)
                         from public.store_items i where i.active), '[]'::jsonb),
    'packs', coalesce((select jsonb_agg(jsonb_build_object('code', c.code, 'coins', c.coins, 'bonus', c.bonus_coins,
                                                           'price_minor', c.price_minor, 'currency', c.currency,
                                                           'title', c.title)
                                        order by c.sort_order, c.code)
                         from public.currency_packs c where c.active), '[]'::jsonb),
    'listings', coalesce((select jsonb_agg(jsonb_build_object('id', l.id, 'item', i.code, 'title', i.title,
                                                              'price', l.price_coins, 'created_at', l.created_at)
                                            order by l.created_at desc)
                            from public.market_listings l join public.store_items i on i.id = l.item_id
                           where l.seller_id = v_id and l.status = 'active'), '[]'::jsonb),
    'ledger', coalesce((select jsonb_agg(jsonb_build_object('delta', x.delta, 'reason', x.reason,
                                                             'balance_after', x.balance_after, 'at', x.created_at)
                                          order by x.id desc)
                          from (select * from public.wallet_ledger where profile_id = v_id
                                 order by id desc limit 20) x), '[]'::jsonb)
  );
end $$;

create or replace function public.svc_store_buy(p_player_key text, p_item_code text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_row record;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  select * into v_row from public._store_buy(v_id, p_item_code);
  return jsonb_build_object('code', p_item_code, 'balance', v_row.balance);
end $$;

create or replace function public.svc_equip_skin(p_player_key text, p_item_code text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_code text;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  v_code := public._equip_skin(v_id, p_item_code);
  return jsonb_build_object('equipped', v_code);
end $$;

create or replace function public.svc_market_board(p_player_key text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := case when p_player_key is null then null else public.svc_ensure_profile(p_player_key, null) end;

  return jsonb_build_object(
    'listings', coalesce((select jsonb_agg(jsonb_build_object(
                            'id', l.id, 'item', i.code, 'title', i.title, 'kind', i.kind,
                            'payload', i.payload, 'price', l.price_coins,
                            'seller', sp.display_name, 'mine', l.seller_id = v_id,
                            'created_at', l.created_at) order by l.price_coins, l.created_at)
                          from public.market_listings l
                          join public.store_items i on i.id = l.item_id
                          join public.profiles sp on sp.id = l.seller_id
                         where l.status = 'active'
                           and (v_id is null or not exists (
                                 select 1 from public.player_blocks b
                                  where (b.blocker_id = v_id and b.blocked_id = l.seller_id)
                                     or (b.blocker_id = l.seller_id and b.blocked_id = v_id)))), '[]'::jsonb),
    'mine', coalesce((select jsonb_agg(jsonb_build_object('id', l.id, 'item', i.code, 'title', i.title,
                                                          'price', l.price_coins, 'created_at', l.created_at)
                                       order by l.created_at desc)
                        from public.market_listings l join public.store_items i on i.id = l.item_id
                       where l.seller_id = v_id and l.status = 'active'), '[]'::jsonb)
  );
end $$;

create or replace function public.svc_market_list(p_player_key text, p_item_code text, p_price bigint)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_listing uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  v_listing := public._market_list(v_id, p_item_code, p_price);
  return jsonb_build_object('listing', v_listing, 'item', p_item_code, 'price', p_price);
end $$;

create or replace function public.svc_market_cancel(p_player_key text, p_listing uuid)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  perform public._market_cancel(v_id, p_listing);
  return jsonb_build_object('cancelled', true, 'listing', p_listing);
end $$;

create or replace function public.svc_market_buy(p_player_key text, p_listing uuid)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_row record;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  select * into v_row from public._market_buy(v_id, p_listing);
  return jsonb_build_object('item', v_row.item_code, 'balance', v_row.balance, 'listing', p_listing);
end $$;

create or replace function public.svc_friends(p_player_key text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);

  return jsonb_build_object(
    'friends', coalesce((select jsonb_agg(jsonb_build_object('key', f.player_key, 'name', f.display_name,
                                                             'skin', f.avatar_skin, 'online', f.online)
                                          order by f.online desc, f.display_name)
                           from public._friend_list(v_id) f), '[]'::jsonb),
    'incoming', coalesce((select jsonb_agg(jsonb_build_object('id', r.id, 'name', p.display_name,
                                                              'key', p.player_key, 'message', r.message,
                                                              'at', r.created_at)
                                           order by r.created_at desc)
                            from public.friend_requests r join public.profiles p on p.id = r.from_id
                           where r.to_id = v_id), '[]'::jsonb),
    'outgoing', coalesce((select jsonb_agg(jsonb_build_object('id', r.id, 'name', p.display_name,
                                                              'key', p.player_key, 'at', r.created_at)
                                           order by r.created_at desc)
                            from public.friend_requests r join public.profiles p on p.id = r.to_id
                           where r.from_id = v_id), '[]'::jsonb)
  );
end $$;

create or replace function public.svc_friend_request(p_player_key text, p_target_key text, p_message text default null)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_result text;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  v_result := public._friend_request(v_id, p_target_key, p_message);
  return jsonb_build_object('result', v_result);
end $$;

create or replace function public.svc_friend_accept(p_player_key text, p_request bigint)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid; v_result text;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  v_result := public._friend_accept(v_id, p_request);
  return jsonb_build_object('result', v_result);
end $$;

create or replace function public.svc_friend_decline(p_player_key text, p_request bigint)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  return jsonb_build_object('result', public._friend_decline(v_id, p_request));
end $$;

create or replace function public.svc_friend_remove(p_player_key text, p_other_key text)
returns jsonb
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_id uuid;
begin
  perform public.assert_service_role();
  v_id := public.svc_ensure_profile(p_player_key, null);
  return jsonb_build_object('result', public._friend_remove(v_id, p_other_key));
end $$;

-- ---------------------------------------------------------------------------
-- Grants: the impl layer is internal, svc_* belongs to the service role only.
-- ---------------------------------------------------------------------------
revoke all on function public.svc_ensure_profile(text, text) from public, anon, authenticated;
revoke all on function public.svc_profile_id(text) from public, anon, authenticated;
revoke all on function public.svc_state(text) from public, anon, authenticated;
revoke all on function public.svc_store_buy(text, text) from public, anon, authenticated;
revoke all on function public.svc_equip_skin(text, text) from public, anon, authenticated;
revoke all on function public.svc_market_board(text) from public, anon, authenticated;
revoke all on function public.svc_market_list(text, text, bigint) from public, anon, authenticated;
revoke all on function public.svc_market_cancel(text, uuid) from public, anon, authenticated;
revoke all on function public.svc_market_buy(text, uuid) from public, anon, authenticated;
revoke all on function public.svc_friends(text) from public, anon, authenticated;
revoke all on function public.svc_friend_request(text, text, text) from public, anon, authenticated;
revoke all on function public.svc_friend_accept(text, bigint) from public, anon, authenticated;
revoke all on function public.svc_friend_decline(text, bigint) from public, anon, authenticated;
revoke all on function public.svc_friend_remove(text, text) from public, anon, authenticated;

grant execute on function public.svc_ensure_profile(text, text) to service_role;
grant execute on function public.svc_profile_id(text) to service_role;
grant execute on function public.svc_state(text) to service_role;
grant execute on function public.svc_store_buy(text, text) to service_role;
grant execute on function public.svc_equip_skin(text, text) to service_role;
grant execute on function public.svc_market_board(text) to service_role;
grant execute on function public.svc_market_list(text, text, bigint) to service_role;
grant execute on function public.svc_market_cancel(text, uuid) to service_role;
grant execute on function public.svc_market_buy(text, uuid) to service_role;
grant execute on function public.svc_friends(text) to service_role;
grant execute on function public.svc_friend_request(text, text, text) to service_role;
grant execute on function public.svc_friend_accept(text, bigint) to service_role;
grant execute on function public.svc_friend_decline(text, bigint) to service_role;
grant execute on function public.svc_friend_remove(text, text) to service_role;

revoke all on function public._store_buy(uuid, text) from public, anon, authenticated;
revoke all on function public._equip_skin(uuid, text) from public, anon, authenticated;
revoke all on function public._market_list(uuid, text, bigint) from public, anon, authenticated;
revoke all on function public._market_cancel(uuid, uuid) from public, anon, authenticated;
revoke all on function public._market_buy(uuid, uuid) from public, anon, authenticated;
revoke all on function public._friend_request(uuid, text, text) from public, anon, authenticated;
revoke all on function public._friend_accept(uuid, bigint) from public, anon, authenticated;
revoke all on function public._friend_decline(uuid, bigint) from public, anon, authenticated;
revoke all on function public._friend_remove(uuid, text) from public, anon, authenticated;
revoke all on function public._friend_list(uuid) from public, anon, authenticated;

-- ---------------------------------------------------------------------------
-- Payments after the bridge: a webhook may credit a player whose profile does
-- not exist yet, so the key-based entry point creates it first.
-- ---------------------------------------------------------------------------
create or replace function public.svc_topup_credit_key(
  p_provider_ref text, p_player_key text, p_pack text, p_provider text,
  p_amount_minor integer, p_currency char(3))
returns bigint
language plpgsql security definer set search_path = public, pg_temp as $$
declare v_profile uuid;
begin
  perform public.assert_service_role();
  v_profile := public.svc_ensure_profile(p_player_key, null);
  return public.svc_topup_credit(p_provider_ref, v_profile, p_pack, p_provider, p_amount_minor, p_currency);
end $$;

revoke all on function public.svc_topup_credit_key(text, text, text, text, integer, char) from public, anon, authenticated;
grant execute on function public.svc_topup_credit_key(text, text, text, text, integer, char) to service_role;
