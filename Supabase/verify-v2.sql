-- End-to-end check of the game bridge (v2). Rolled back: production data is untouched.
-- Every step goes through the same entry points the game server uses.
begin;
create temporary table b_log (step serial, line text) on commit drop;

do $$
declare
  v_a text := repeat('c', 32);
  v_b text := repeat('d', 32);
  v_state jsonb;
  v_listing uuid;
  v_balance bigint;
  v_ok boolean;
  v_result text;
begin
  -- 0. The bridge is not reachable by players, only by the service role.
  perform set_config('request.jwt.claims', json_build_object('role', 'authenticated')::text, true);
  begin
    perform public.svc_state(v_a);
    raise exception 'svc_state must not be callable as an authenticated user';
  exception when insufficient_privilege then
    insert into b_log (line) values ('PASS players cannot call svc_* directly');
  end;

  -- 1. First contact creates the profile with exactly zero coins.
  perform set_config('request.jwt.claims', json_build_object('role', 'service_role')::text, true);
  perform public.svc_ensure_profile(v_a, 'Проверка А');
  perform public.svc_ensure_profile(v_b, 'Проверка Б');
  v_state := public.svc_state(v_a);
  if (v_state->>'balance')::bigint <> 0 then
    raise exception 'a new player must start with 0 coins, got %', v_state->>'balance';
  end if;
  if jsonb_array_length(v_state->'owned') <> 0 then raise exception 'new player owns items'; end if;
  insert into b_log (line) values ('PASS new player starts with 0 coins and no items');

  -- 2. Buying without coins fails and changes nothing.
  begin
    perform public.svc_store_buy(v_a, 'skin_ember');
    raise exception 'purchase without coins must fail';
  exception when others then
    if sqlerrm not like '%insufficient coins%' then raise; end if;
    insert into b_log (line) values ('PASS purchase without coins is refused');
  end;

  -- 3. A paid pack credits coins exactly once (replay is ignored).
  update public.currency_packs set active = true where code = 'pouch';
  v_balance := public.svc_topup_credit('test:bridge-1', (select id from public.profiles where player_key = v_a),
                                       'pouch', 'test', 199, 'USD');
  if v_balance <> 500 then raise exception 'expected 500 coins after the pouch, got %', v_balance; end if;
  v_balance := public.svc_topup_credit('test:bridge-1', (select id from public.profiles where player_key = v_a),
                                       'pouch', 'test', 199, 'USD');
  if v_balance is not null then raise exception 'a replayed top-up must not credit twice'; end if;
  if (public.svc_state(v_a)->>'balance')::bigint <> 500 then raise exception 'balance drifted after replay'; end if;
  insert into b_log (line) values ('PASS paid pack credits once, replays are ignored');

  -- 4. Buying a skin debits exactly its price and grants it.
  v_state := public.svc_store_buy(v_a, 'skin_ember');
  if (v_state->>'balance')::bigint <> 50 then raise exception 'expected 50 coins left, got %', v_state->>'balance'; end if;
  v_state := public.svc_state(v_a);
  if not exists (select 1 from jsonb_array_elements(v_state->'owned') e where e->>'code' = 'skin_ember') then
    raise exception 'skin was paid for but not granted';
  end if;
  insert into b_log (line) values ('PASS buying a skin debits the price and grants the item');

  -- 5. Equipping a skin is remembered on the profile.
  v_state := public.svc_equip_skin(v_a, 'skin_ember');
  if v_state->>'equipped' <> 'skin_ember' then raise exception 'skin was not equipped'; end if;
  if (public.svc_state(v_a)->'profile'->>'avatar_skin') <> 'skin_ember' then
    raise exception 'equipped skin did not persist';
  end if;
  begin
    perform public.svc_equip_skin(v_a, 'cape_aurora');
    raise exception 'equipping an unowned skin must fail';
  exception when others then
    if sqlerrm not like '%skin not owned%' then raise; end if;
    insert into b_log (line) values ('PASS an unowned skin cannot be equipped');
  end;

  -- 6. Marketplace: listing escrows, buying moves coins minus the 5% fee.
  v_listing := (public.svc_market_list(v_a, 'skin_ember', 120) ->> 'listing')::uuid;
  v_state := public.svc_state(v_a);
  if exists (select 1 from jsonb_array_elements(v_state->'owned') e where e->>'code' = 'skin_ember') then
    raise exception 'a listed item must leave the inventory (escrow)';
  end if;
  begin
    perform public.svc_market_buy(v_a, v_listing);
    raise exception 'a seller must not buy his own listing';
  exception when others then
    if sqlerrm not like '%cannot buy your own listing%' then raise; end if;
  end;

  -- B has no coins yet: give B the price through a second pack.
  update public.currency_packs set active = true where code = 'satchel';
  perform public.svc_topup_credit('test:bridge-2', (select id from public.profiles where player_key = v_b),
                                  'satchel', 'test', 499, 'USD');
  v_state := public.svc_market_buy(v_b, v_listing);
  if v_state->>'item' <> 'skin_ember' then raise exception 'buyer did not receive the item'; end if;

  -- A: 50 coins + (120 - 5% = 114) = 164. B: 1300 - 120 = 1180.
  v_balance := (public.svc_state(v_a)->>'balance')::bigint;
  if v_balance <> 164 then raise exception 'seller balance should be 164, got %', v_balance; end if;
  v_balance := (public.svc_state(v_b)->>'balance')::bigint;
  if v_balance <> 1180 then raise exception 'buyer balance should be 1180, got %', v_balance; end if;
  if exists (select 1 from public.market_listings where status = 'active' and id = v_listing) then
    raise exception 'the sold listing is still active';
  end if;
  insert into b_log (line) values ('PASS marketplace escrows, sells and takes the 5% fee');

  -- 7. Friends: request, accept, list, remove.
  v_result := public.svc_friend_request(v_a, v_b, 'привет') ->> 'result';
  if v_result <> 'sent' then raise exception 'friend request result: %', v_result; end if;
  declare v_req bigint := (public.svc_friends(v_b)->'incoming'->0->>'id')::bigint;
  begin
    if v_req is null then raise exception 'a request must reach the other player'; end if;
    v_result := public.svc_friend_accept(v_b, v_req) ->> 'result';
    if v_result <> 'accepted' then raise exception 'accept result: %', v_result; end if;
    if jsonb_array_length(public.svc_friends(v_a)->'friends') <> 1 then
      raise exception 'friendship was not created';
    end if;
    if (public.svc_friends(v_a)->'friends'->0->>'name') <> 'Проверка Б' then
      raise exception 'friend list shows the wrong name';
    end if;
    v_result := public.svc_friend_remove(v_a, v_b) ->> 'result';
    if v_result <> 'removed' or jsonb_array_length(public.svc_friends(v_a)->'friends') <> 0 then
      raise exception 'friend removal failed';
    end if;
  end;
  insert into b_log (line) values ('PASS friends request, accept, list and remove');

  -- 8. An unknown or self player key answers exactly like a real one.
  v_result := public.svc_friend_request(v_a, repeat('e', 32), null) ->> 'result';
  if v_result <> 'sent' then raise exception 'unknown key must look like a success'; end if;
  v_result := public.svc_friend_request(v_a, v_a, null) ->> 'result';
  if v_result <> 'sent' then raise exception 'self request must look like a success'; end if;
  insert into b_log (line) values ('PASS unknown and self keys are indistinguishable');

  -- 9. A malformed player key is rejected before touching the database.
  begin
    perform public.svc_ensure_profile('not-a-key', 'x');
    raise exception 'invalid key must be rejected';
  exception when others then
    if sqlerrm not like '%invalid player key%' then raise; end if;
    insert into b_log (line) values ('PASS malformed player keys are rejected');
  end;

  -- 10. Wallets stay invisible to a signed-in player: RLS filters every row,
  --     even though the rows exist for the service role.
  perform set_config('role', 'authenticated', true);
  perform set_config('request.jwt.claims', json_build_object('role', 'authenticated')::text, true);
  select count(*) into v_balance from public.wallets;
  perform set_config('role', 'none', true);
  if v_balance <> 0 then
    raise exception 'authenticated callers saw % wallet rows', v_balance;
  end if;
  if (select count(*) from public.wallets) = 0 then
    raise exception 'the test itself is broken: wallets should exist for the service role';
  end if;
  insert into b_log (line) values ('PASS wallets stay private under RLS');
end $$;

select step, line from b_log order by step;
rollback;
