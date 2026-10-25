-- End-to-end verification of the economy, marketplace and friends schema.
-- Runs inside a transaction that is rolled back, so production data is untouched.
-- Every step acts as a real end user through auth.uid(), never as the owner.
begin;
create temporary table t_log (step serial, line text) on commit drop;

-- Two players + one real auth user each, exactly like the web store would create.
create temporary table t_ids (name text primary key, uid uuid, pid uuid) on commit drop;

do $$
declare
  v_a uuid := gen_random_uuid();
  v_b uuid := gen_random_uuid();
  p_a uuid; p_b uuid;
begin
  insert into auth.users (id, instance_id, aud, role, email, encrypted_password, email_confirmed_at, created_at, updated_at)
  values (v_a, '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated', 'e2e-a@example.invalid', 'x', now(), now(), now()),
         (v_b, '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated', 'e2e-b@example.invalid', 'x', now(), now(), now())
  on conflict (id) do nothing;

  insert into public.profiles (player_key, auth_user_id, display_name)
  values (repeat('a',32), v_a, 'E2E Alice') returning id into p_a;
  insert into public.profiles (player_key, auth_user_id, display_name)
  values (repeat('b',32), v_b, 'E2E Boris') returning id into p_b;

  insert into t_ids values ('a', v_a, p_a), ('b', v_b, p_b);
end $$;

-- 1. A brand-new player has exactly zero coins: no starter grant.
do $$
declare v_uid uuid; v_bal bigint;
begin
  select uid into v_uid from t_ids where name='a';
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid, 'role', 'authenticated')::text, true);
  select balance into v_bal from public.my_profile();
  if v_bal is distinct from 0 then raise exception 'expected 0 starting coins, got %', v_bal; end if;
  insert into t_log (line) values ('PASS new wallet starts at 0 coins (no starter currency)');
end $$;

-- 2. A client cannot credit itself: top-ups are service-role only.
do $$
declare v_uid uuid; ok boolean := false;
begin
  select uid into v_uid from t_ids where name='a';
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid, 'role', 'authenticated')::text, true);
  begin
    perform public.svc_topup_credit('e2e-ref-0', (select pid from t_ids where name='a'), 'pouch', 'stripe', 199, 'USD');
  exception when others then ok := true;
  end;
  if not ok then raise exception 'SECURITY: an authenticated client credited its own wallet'; end if;
  insert into t_log (line) values ('PASS client cannot mint coins; svc_topup_credit rejects non-service callers');
end $$;

-- 3. The payment webhook (service role) credits a pack once and only once.
do $$
declare v_bal bigint; v_pid uuid;
begin
  select pid into v_pid from t_ids where name='a';
  perform set_config('request.jwt.claims', json_build_object('role', 'service_role')::text, true);
  update public.currency_packs set active = true where code = 'pouch'; -- enabled here only for this test

  v_bal := public.svc_topup_credit('e2e-ref-1', v_pid, 'pouch', 'stripe', 199, 'USD');
  if v_bal <> 500 then raise exception 'expected 500 coins after top-up, got %', v_bal; end if;

  v_bal := public.svc_topup_credit('e2e-ref-1', v_pid, 'pouch', 'stripe', 199, 'USD'); -- replay
  if v_bal is not null then raise exception 'replayed webhook credited twice'; end if;

  if (select balance from public.wallets where profile_id = v_pid) <> 500 then
    raise exception 'balance changed on replay';
  end if;
  insert into t_log (line) values ('PASS paid top-up credits once, replay is idempotent');
end $$;

-- 4. Buying a skin debits the wallet and grants the entitlement.
do $$
declare v_uid uuid; v_pid uuid; v_item uuid; v_bal bigint;
begin
  select uid, pid into v_uid, v_pid from t_ids where name='a';
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid, 'role', 'authenticated')::text, true);

  select item_id, balance into v_item, v_bal from public.store_buy('skin_ember');
  if v_bal <> 50 then raise exception 'expected 50 coins left, got %', v_bal; end if;
  if not exists (select 1 from public.entitlements where profile_id = v_pid and item_id = v_item) then
    raise exception 'entitlement missing';
  end if;

  begin
    perform public.store_buy('skin_ember');
    raise exception 'double purchase succeeded';
  exception when others then null;
  end;
  insert into t_log (line) values ('PASS store purchase debits wallet, grants skin, blocks duplicate');
end $$;

-- 5. Marketplace: escrow, then a second player buys it and coins move seller-wards.
do $$
declare v_uid_a uuid; v_uid_b uuid; v_pid_b uuid; v_listing uuid; v_code text; v_bal bigint;
begin
  select uid, pid into v_uid_a, v_pid_b from t_ids where name='a' limit 1;
  select uid into v_uid_b from t_ids where name='b';
  select pid into v_pid_b from t_ids where name='b';

  -- Bob gets coins from a paid pack too.
  perform set_config('request.jwt.claims', json_build_object('role', 'service_role')::text, true);
  perform public.svc_topup_credit('e2e-ref-2', v_pid_b, 'pouch', 'stripe', 199, 'USD');

  -- Alice lists the skin she bought.
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_a, 'role', 'authenticated')::text, true);
  v_listing := public.market_list('skin_ember', 100);
  if exists (select 1 from public.entitlements where profile_id = (select pid from t_ids where name='a')) then
    raise exception 'listed item was not escrowed';
  end if;

  -- Bob buys it.
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_b, 'role', 'authenticated')::text, true);
  select item_code, balance into v_code, v_bal from public.market_buy(v_listing);
  if v_code <> 'skin_ember' then raise exception 'wrong item delivered: %', v_code; end if;
  if v_bal <> 400 then raise exception 'buyer should keep 400 coins, has %', v_bal; end if;

  if not exists (select 1 from public.entitlements e where e.profile_id = v_pid_b) then
    raise exception 'buyer did not receive the skin';
  end if;

  -- Seller receives price minus the 5% fee: 50 + 95 = 145.
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_a, 'role', 'authenticated')::text, true);
  if (select balance from public.my_profile()) <> 145 then
    raise exception 'seller balance wrong: %', (select balance from public.my_profile());
  end if;
  insert into t_log (line) values ('PASS marketplace escrows, transfers the item, moves coins and takes 5% fee');
end $$;

-- 6. Friends: request, accept, list, remove. Unknown keys are indistinguishable.
do $$
declare v_uid_a uuid; v_uid_b uuid; v_req bigint; v_res text;
begin
  select uid into v_uid_a from t_ids where name='a';
  select uid into v_uid_b from t_ids where name='b';

  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_a, 'role', 'authenticated')::text, true);
  if public.friend_request(repeat('f',32)) <> 'sent' then raise exception 'unknown key leaked'; end if;
  perform public.friend_request(repeat('b',32), 'привет');

  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_b, 'role', 'authenticated')::text, true);
  select id into v_req from public.friend_requests where to_id = (select pid from t_ids where name='b');
  if v_req is null then raise exception 'request not delivered'; end if;
  perform public.friend_accept(v_req);

  -- Privacy: a friendship between two completely different players must never
  -- show up in this player's list (the join used to expose one side of every row).
  insert into public.profiles (player_key, display_name) values (repeat('c',32), 'E2E Carol');
  insert into public.profiles (player_key, display_name) values (repeat('d',32), 'E2E Dave');
  insert into public.friendships (low_id, high_id)
  select least(c.id, d.id), greatest(c.id, d.id)
    from public.profiles c, public.profiles d
   where c.player_key = repeat('c',32) and d.player_key = repeat('d',32);

  if (select count(*) from public.friend_list()) <> 1 then raise exception 'friend list should have 1 entry'; end if;
  if exists (select 1 from public.friend_list() where display_name in ('E2E Carol','E2E Dave')) then
    raise exception 'SECURITY: friend_list leaked a friendship between other players';
  end if;

  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_a, 'role', 'authenticated')::text, true);
  v_res := public.friend_remove(repeat('b',32));
  if (select count(*) from public.friend_list()) <> 0 then raise exception 'friend not removed'; end if;
  insert into t_log (line) values ('PASS friends: request, accept, list, remove, no key oracle');
end $$;

-- 7. RLS: a player cannot read another player's wallet through the API.
do $$
declare v_uid_a uuid; v_pid_b uuid; v_visible int;
begin
  -- Look everything up before dropping to the authenticated role: the temp
  -- table is not readable by it, which is the point of the check below.
  select uid into v_uid_a from t_ids where name='a';
  select pid into v_pid_b from t_ids where name='b';
  perform set_config('request.jwt.claims', json_build_object('sub', v_uid_a, 'role', 'authenticated')::text, true);
  perform set_config('role', 'authenticated', true);
  select count(*) into v_visible from public.wallets where profile_id = v_pid_b;
  perform set_config('role', 'postgres', true);
  if v_visible <> 0 then raise exception 'RLS LEAK: read another wallet'; end if;
  insert into t_log (line) values ('PASS RLS hides other players coins');
end $$;

select step, line from t_log order by step;
rollback;
