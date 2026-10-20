using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Runtime;
using LestingCraft.World;
using LestingCraft.Networking;
using LestingCraft.Presentation;

namespace LestingCraft.Gameplay
{
    [Serializable]
    public sealed class AnimalDamage
    {
        public int part = 1;
        public string id;
        public long seq;
        public float amount;
    }

    public sealed class WildlifeManager : MonoBehaviour
    {
        const int VisualLimit = 32, PoolLimit = 24;
        SurvivalSession session;
        WildlifeSimulation simulation;
        readonly Dictionary<string, AnimalState> remote = new Dictionary<string, AnimalState>();
        readonly Dictionary<string, AnimalVisual> visuals = new Dictionary<string, AnimalVisual>();
        readonly List<AnimalVisual> pool = new List<AnimalVisual>();
        readonly List<AnimalLoot> pending = new List<AnimalLoot>();
        readonly List<int> expired = new List<int>();
        readonly List<string> remove = new List<string>();
        readonly HashSet<string> visible = new HashSet<string>();
        readonly AnimalObserver[] observers = new AnimalObserver[1];
        readonly AnimalState[] candidates = new AnimalState[VisualLimit];
        readonly float[] distances = new float[VisualLimit];
        WildlifeSimulation.Ground groundCallback;
        Action<string, float> damageCallback;
        float spawnClock, tickClock, attackAt = -10;
        long damageSequence;
        int requestId;

        sealed class QueuedAttack { public int slot, item; public float at; }
        readonly Dictionary<int, QueuedAttack> queuedAttacks = new Dictionary<int, QueuedAttack>();

        bool Enabled
        {
            get
            {
                return session && (int)session.World.Dimension == 0 &&
                    (!session.Game.Online || session.World.ContentVersion >= 2);
            }
        }

        public void Initialize(SurvivalSession s)
        {
            session = s;
            simulation = new WildlifeSimulation(s.World.Seed, s.Data.wildlife, s.World.ContentVersion);
            simulation.SurfaceAt = SurfaceAt;
            groundCallback = Ground;
            damageCallback = DealDamage;
        }

        int SurfaceAt(int x, int z)
        {
            int y = TerrainGenerator.Height(x, z, session.World.Seed, session.World.Dimension);
            return session.World.GetBlock(x, y, z);
        }

        void DealDamage(string id, float amount)
        {
            session.Impact(amount, BodyPart.LeftLeg, -session.Player.transform.forward);
        }

        public void Reject(int request) { queuedAttacks.Remove(request); }
        public WildlifeSave Save() { return simulation.Save(); }
        IEnumerable<AnimalState> States
        {
            get { return session.Game.Online ? (IEnumerable<AnimalState>)remote.Values : simulation.Animals; }
        }

        public void Apply(NetPacket packet)
        {
            QueuedAttack attack;
            if (packet.type == "animal_hit" && packet.id == session.Game.LocalPeerId &&
                queuedAttacks.TryGetValue(packet.requestId, out attack))
            {
                queuedAttacks.Remove(packet.requestId);
                if (!session.Creative && attack.slot >= 0 && attack.slot < session.Inventory.slots.Length &&
                    session.Inventory.slots[attack.slot].item == attack.item)
                {
                    int selected = session.Inventory.selected;
                    session.Inventory.selected = attack.slot;
                    session.Inventory.WearTool();
                    session.Inventory.selected = selected;
                }
            }
            if (packet.animals != null)
            {
                remote.Clear();
                int limit = session.World.ContentVersion >= 7 ? 12 : 3;
                foreach (AnimalState a in packet.animals)
                    if (a != null && a.id != null && !a.dead && a.species >= 0 && a.species < limit &&
                        Finite(a.x) && Finite(a.y) && Finite(a.z) && Finite(a.yaw) && remote.Count < WildlifeSimulation.MaxAnimals)
                        remote[a.id] = a;
            }
            if (packet.loot != null && (packet.type == "welcome" || packet.id == session.Game.LocalPeerId))
                foreach (AnimalLoot l in packet.loot)
                    if (l != null && l.id != null && !pending.Exists(x => x.id == l.id)) pending.Add(l);
            if (packet.damageEvents == null) return;
            Array.Sort(packet.damageEvents, (a, b) => a.seq.CompareTo(b.seq));
            foreach (AnimalDamage d in packet.damageEvents)
                if (d.id == session.Game.LocalPeerId && d.seq > damageSequence)
                {
                    damageSequence = d.seq;
                    session.Impact(d.amount, (BodyPart)Mathf.Clamp(d.part, 0, 5), -session.Player.transform.forward);
                }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        bool AddLoot(AnimalLoot l)
        {
            var inv = session.Inventory.Clone();
            if (!inv.Add(135, l.meat) || !inv.Add(136, l.hide))
            {
                session.Notice = "Для мяса и шкуры нужно место в инвентаре";
                return false;
            }
            session.Inventory.slots = inv.slots;
            session.Inventory.revision++;
            session.Progress.Kill(l);
            return true;
        }

        public bool Intersect(Ray ray, float limit, out string id, int weaponOverride = 0)
        {
            id = null;
            float reach = Weapons.Get(weaponOverride > 0 ? weaponOverride : session.Inventory.Active.item).Reach;
            float nearest = Mathf.Min(reach, limit);
            if (!Enabled) return false;
            foreach (AnimalState a in States)
            {
                if (a.dead) continue;
                var center = new Vector3(a.x, a.y + WildlifeSimulation.Height(a.species) * .5f, a.z);
                if ((center - ray.origin).sqrMagnitude > reach * reach) continue;
                Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0, a.yaw, 0));
                var localRay = new Ray(inverse * (ray.origin - center), inverse * ray.direction);
                var bounds = new Bounds(Vector3.zero, new Vector3(WildlifeSimulation.Radius(a.species) * 2,
                    WildlifeSimulation.Height(a.species), WildlifeSimulation.Length(a.species)));
                float distance;
                if (bounds.IntersectRay(localRay, out distance) && distance >= 0 && distance < nearest)
                {
                    id = a.id; nearest = distance;
                }
            }
            return id != null;
        }

        public bool Overlaps(Vector3Int cell)
        {
            if (!Enabled) return false;
            var block = new Bounds((Vector3)cell + Vector3.one * .5f, Vector3.one * .99f);
            foreach (AnimalState a in States)
            {
                if (a.dead) continue;
                float angle = a.yaw * Mathf.Deg2Rad;
                float width = WildlifeSimulation.Radius(a.species) * 2, length = WildlifeSimulation.Length(a.species);
                var size = new Vector3(Mathf.Abs(Mathf.Cos(angle)) * width + Mathf.Abs(Mathf.Sin(angle)) * length,
                    WildlifeSimulation.Height(a.species),
                    Mathf.Abs(Mathf.Sin(angle)) * width + Mathf.Abs(Mathf.Cos(angle)) * length);
                if (block.Intersects(new Bounds(new Vector3(a.x, a.y + size.y * .5f, a.z), size))) return true;
            }
            return false;
        }

        public int NewArrowShot() { return ++requestId; }

        public void Attack(string id, bool arrow = false, int arrowRequest = 0)
        {
            if (!session.CanInteract || !arrow && Time.time < attackAt || !Enabled) return;
            attackAt = Time.time + Weapons.Get(session.Inventory.Active.item).Cooldown / 1000f / session.Vitals.body.Dexterity;
            session.ResetMining();
            int weapon = arrow ? 164 : session.Inventory.Active.item;
            if (session.Game.Online)
            {
                if (queuedAttacks.Count >= 4) return;
                int request = arrow ? arrowRequest : ++requestId;
                queuedAttacks[request] = new QueuedAttack
                {
                    slot = arrow ? -1 : session.Inventory.selected, item = weapon, at = Time.unscaledTime
                };
                session.Game.SendAnimalHit(id, weapon == 102 || weapon == 103 || Weapons.Has(weapon) ? weapon : 0, request);
                return;
            }
            AnimalState a = simulation.Animals.Find(q => q.id == id && !q.dead);
            if (a == null) return;
            if (a.health <= WildlifeSimulation.Damage(weapon))
            {
                var copy = session.Inventory.Clone();
                AnimalLoot loot = WildlifeSimulation.Loot(a);
                if (!session.Creative && (!copy.Add(135, loot.meat) || !copy.Add(136, loot.hide)))
                {
                    session.Notice = "Освободите место для добычи";
                    return;
                }
            }
            Vector3 p = session.Player.Camera.transform.position;
            AnimalLoot result = simulation.Hit(id, weapon, p.x, p.y, p.z);
            if (!session.Creative && !arrow) session.Inventory.WearTool();
            if (result != null)
            {
                if (!session.Creative) AddLoot(result);
                session.Checkpoint();
            }
        }

        bool Solid(int x, int y, int z) { return BlockRegistry.IsSolid(session.World.GetBlock(x, y, z)); }

        bool SpawnSurface(int block)
        {
            if (block == 3 || block == 2 || block == 14) return true;
            return session.World.ContentVersion >= 7 &&
                (block == 1 || block == 6 || block == 27 || block == 32 || block == 33 || block == 34 || block == 43);
        }

        float Ground(float x, float z, float previousY, float radius, float height)
        {
            var w = session.World;
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            if (!w.Contains(ix, 1, iz)) return float.NaN;
            int natural = TerrainGenerator.Height(ix, iz, w.Seed, w.Dimension) + 1;
            int hi = previousY < 0 ? natural + 1 : Mathf.FloorToInt(previousY) + 1;
            int lo = previousY < 0 ? natural - 2 : Mathf.FloorToInt(previousY) - 1;
            for (int y = hi; y >= lo; y--)
            {
                bool valid = true, supported = false;
                for (int k = 0; k < 5 && valid; k++)
                {
                    int ax = Mathf.FloorToInt(x + (k == 0 ? 0 : k % 2 == 0 ? radius : -radius));
                    int az = Mathf.FloorToInt(z + (k == 0 ? 0 : k < 3 ? -radius : radius));
                    if (!w.Contains(ax, y, az)) { valid = false; break; }
                    bool support = Solid(ax, y - 1, az);
                    supported |= support;
                    int below = w.GetBlock(ax, y - 1, az);
                    if (!support && (previousY < 0 || below == 7 || below == 8 || !Solid(ax, y - 2, az)))
                    {
                        valid = false; break;
                    }
                    if (previousY < 0 && !SpawnSurface(below)) { valid = false; break; }
                    for (int cy = y; cy < Mathf.CeilToInt(y + height); cy++)
                        if (w.GetBlock(ax, cy, az) != 0) { valid = false; break; }
                }
                if (valid && supported && y > 41 && y < 100) return y;
            }
            return float.NaN;
        }

        AnimalVisual Acquire(AnimalState a)
        {
            AnimalVisual visual = null;
            for (int i = pool.Count - 1; i >= 0; i--)
                if (pool[i] && pool[i].Species == a.species)
                {
                    visual = pool[i]; pool.RemoveAt(i); break;
                }
            if (!visual)
            {
                var obj = new GameObject(AnimalVisual.SpeciesNames[a.species]);
                obj.transform.SetParent(session.World.transform, false);
                visual = obj.AddComponent<AnimalVisual>();
                visual.Build(a.species);
                var collision = obj.AddComponent<BoxCollider>();
                collision.center = Vector3.up * WildlifeSimulation.Height(a.species) * .5f;
                collision.size = new Vector3(WildlifeSimulation.Radius(a.species) * 2,
                    WildlifeSimulation.Height(a.species), WildlifeSimulation.Length(a.species));
            }
            visual.transform.position = new Vector3(a.x, a.y, a.z);
            visual.transform.rotation = Quaternion.Euler(0, a.yaw, 0);
            visual.gameObject.SetActive(true);
            visual.ResetMotion();
            return visual;
        }

        void Release(AnimalVisual visual)
        {
            if (!visual) return;
            visual.gameObject.SetActive(false);
            if (pool.Count < PoolLimit) pool.Add(visual);
            else Destroy(visual.gameObject);
        }

        void HideAll()
        {
            foreach (var pair in visuals) Release(pair.Value);
            visuals.Clear();
        }

        void Update()
        {
            if (!Enabled || !session.World.Ready) { HideAll(); return; }
            if (!session.Game.Online && session.Game.Paused && !session.Game.InventoryOpen) return;
            if (session.Data.campaign.runEnded) { HideAll(); return; }

            expired.Clear();
            foreach (var attack in queuedAttacks)
                if (Time.unscaledTime - attack.Value.at > 3) expired.Add(attack.Key);
            foreach (int id in expired) queuedAttacks.Remove(id);
            Vector3 p = session.Player.Camera.transform.position;

            if (!session.Game.Online)
            {
                observers[0] = new AnimalObserver { id = "local", x = p.x, y = p.y, z = p.z, alive = !session.Vitals.dead };
                spawnClock -= Time.deltaTime;
                tickClock += Time.deltaTime;
                if (spawnClock <= 0)
                {
                    spawnClock = 2;
                    if (observers[0].alive) simulation.Spawn(observers[0], groundCallback);
                }
                if (tickClock >= .1f)
                {
                    simulation.Step(Mathf.Min(.25f, tickClock), observers, groundCallback, damageCallback);
                    tickClock = 0;
                }
            }
            else if (session.CanInteract)
            {
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    AnimalLoot l = pending[i];
                    if (Array.IndexOf(session.Data.player.claimedAnimalLoot, l.id) >= 0) { pending.RemoveAt(i); continue; }
                    if (!AddLoot(l)) break;
                    var ids = new List<string>(session.Data.player.claimedAnimalLoot) { l.id };
                    session.Data.player.claimedAnimalLoot = ids.ToArray();
                    pending.RemoveAt(i);
                }
            }

            // Fixed-capacity nearest selection: no per-frame sorting allocations,
            // no renderer updates or animation work for the other 224 animals.
            int count = 0;
            foreach (AnimalState a in States)
            {
                if (a.dead) continue;
                float d = (new Vector3(a.x, a.y, a.z) - p).sqrMagnitude;
                if (d >= 3136) continue;
                float score = d * (visuals.ContainsKey(a.id) ? .9f : 1);
                int at = count;
                while (at > 0 && distances[at - 1] > score) at--;
                if (at >= VisualLimit) continue;
                int end = Mathf.Min(count, VisualLimit - 1);
                for (int j = end; j > at; j--) { candidates[j] = candidates[j - 1]; distances[j] = distances[j - 1]; }
                candidates[at] = a; distances[at] = score;
                if (count < VisualLimit) count++;
            }
            visible.Clear();
            for (int i = 0; i < count; i++) visible.Add(candidates[i].id);
            remove.Clear();
            foreach (var pair in visuals)
                if (!visible.Contains(pair.Key)) { Release(pair.Value); remove.Add(pair.Key); }
            foreach (string id in remove) visuals.Remove(id);

            float positionBlend = 1 - Mathf.Exp(-Time.deltaTime * 12);
            float rotationBlend = 1 - Mathf.Exp(-Time.deltaTime * 9);
            for (int i = 0; i < count; i++)
            {
                AnimalState a = candidates[i];
                AnimalVisual visual;
                if (!visuals.TryGetValue(a.id, out visual))
                {
                    visual = Acquire(a);
                    visuals.Add(a.id, visual);
                }
                Vector3 target = new Vector3(a.x, a.y, a.z);
                visual.transform.position = Vector3.Lerp(visual.transform.position, target, positionBlend);
                visual.transform.rotation = Quaternion.Slerp(visual.transform.rotation,
                    Quaternion.Euler(0, a.yaw, 0), rotationBlend);
                float distance = Vector3.Distance(target, p);
                visual.SetViewDistance(distance);
                visual.Animate(Time.deltaTime, distance);
                candidates[i] = null;
            }
        }

        void OnDisable() { HideAll(); }

        void OnDestroy()
        {
            foreach (var pair in visuals) if (pair.Value) Destroy(pair.Value.gameObject);
            foreach (AnimalVisual visual in pool) if (visual) Destroy(visual.gameObject);
            visuals.Clear(); pool.Clear();
        }
    }
}
