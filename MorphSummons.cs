using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Coop;
using Drova_Modding_API.Systems.Morphing;
using Drova_Modding_API.Systems.Spawning;
using Drova_Modding_API.Systems.Spawning.Modules;
using Il2CppDrova;
using Il2CppDrova.Alignment;
using Il2CppDrova.DeathBehaviours;
using MelonLoader;
using UnityEngine;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace MageDrova
{
    /// <summary>
    /// Every Silverback, a teammate's too, leads two famished rippers. They fight whatever their Silverback fights and die when its morph ends.
    /// The world authority spawns the packs. In co-op the other machines get copies from the co-op mod and give them the same loyalty.
    /// </summary>
    internal static class MorphSummons
    {
        private const int PackSize = 2;
        private const float SpawnRadius = 40f;
        private const float UpdateSeconds = 0.5f;

        /// <summary>
        /// An idle summon further away than this is put back next to its Silverback. Creatures have no follow routine to walk with.
        /// </summary>
        private const float LeashDistance = 400f;

        /// <summary>
        /// How far from its Silverback a copy the host announced may stand. The host spawns it right next to it.
        /// </summary>
        private const float CopyReach = 150f;

        /// <summary>
        /// A copy no Silverback claimed by then is a famished ripper of some other mod.
        /// </summary>
        private const float ClaimSeconds = 5f;

        /// <summary>
        /// Friendly with humans and wild animals alike, so the pack only fights the kinds its Silverback turned on.
        /// </summary>
        private const string BaseAlignment = "Alignment_Neutral";

        private static readonly string Summoner = AddressableAccess.Creatures.EliteRipper.AssetGUID;
        private static readonly string Summon = AddressableAccess.Creatures.HungryRipper.AssetGUID;
        private static readonly Dictionary<IntPtr, Pack> PackByOwner = new();
        private static readonly List<(Actor Copy, float Since)> Unclaimed = [];
        private static readonly List<CoopPlayer> Players = [];
        private static readonly List<Actor> Owners = [];
        private static readonly List<IntPtr> Gone = [];

        /// <summary>
        /// The entity infos of every player's body. The co-op mod gives each teammate's body its own copy.
        /// </summary>
        private static readonly Il2CppCollections.List<EntityInfo> PlayerKinds = new();

        /// <summary>
        /// A copy of the famished ripper's entity info for the summons. Hitting a wild famished ripper makes the Silverback an enemy of
        /// that kind, and a summon of the same kind would turn on it too.
        /// </summary>
        private static EntityInfo? _kind;

        private static AlignmentContainer? _baseAlignment;
        private static long _playerKindsKey;
        private static float _nextUpdate;

        /// <summary>
        /// The living summons this machine knows, for the test agent.
        /// </summary>
        internal static int Alive => PackByOwner.Values.Sum(pack => pack.Summons.Count(summon => summon != null && !summon.GetHealth().IsDead));

        internal static void Register()
        {
            ActorSpawnAccess.OnActorSpawned += OnActorSpawned;
        }

        private static void OnActorSpawned(Actor actor, string assetGuid)
        {
            // A copy of the host's pack. The teammate it follows may not stand here yet.
            if (assetGuid == Summon && !CoopAccess.IsWorldAuthority)
            {
                Unclaimed.Add((actor, Time.time));
            }
        }

        /// <summary>
        /// Called every frame. Raises a pack for each new Silverback, disbands those whose morph ended and keeps the rest together.
        /// </summary>
        internal static void Update()
        {
            if (Time.time < _nextUpdate || !MorphManager.TryGetCreature(Summoner, out Actor? silverback) || !MorphManager.TryGetCreature(Summon, out Actor? prefab))
            {
                return;
            }

            _nextUpdate = Time.time + UpdateSeconds;
            FindOwners(silverback.GetEntityInfo());
            DisbandLost();
            bool authority = CoopAccess.IsWorldAuthority;
            foreach (Actor owner in Owners)
            {
                if (PackByOwner.ContainsKey(owner.Pointer))
                {
                    continue;
                }

                Pack pack = new(owner);
                PackByOwner[owner.Pointer] = pack;
                for (int i = 0; authority && i < PackSize; i++)
                {
                    // The asset id lets the co-op mod build the summon on every other machine.
                    if (ActorSpawnAccess.TrySpawn(prefab.gameObject, NextTo(owner), out Actor? summon, Summon))
                    {
                        Enlist(pack, summon);
                    }
                }
            }

            Claim();
            foreach (Pack pack in PackByOwner.Values)
            {
                Swear(pack);
                if (authority)
                {
                    Follow(pack);
                }
            }
        }

        /// <summary>
        /// Collects every player's body that is a Silverback, and the entity infos of all player bodies.
        /// </summary>
        private static void FindOwners(EntityInfo silverback)
        {
            Owners.Clear();
            PlayerKinds.Clear();
            _playerKindsKey = 0;
            CoopAccess.GetPlayers(Players);
            foreach (CoopPlayer player in Players)
            {
                if (!CoopAccess.TryGetPlayerActor(player.Id, out Actor? body))
                {
                    continue;
                }

                EntityInfo info = body.GetEntityInfo();
                if (info == null)
                {
                    continue;
                }

                PlayerKinds.Add(info);
                _playerKindsKey += info.Pointer.ToInt64();

                // A teammate's creature carries a copy of the creature's entity info.
                if (info == silverback || info.name == silverback.name + "(Clone)")
                {
                    Owners.Add(body);
                }
            }
        }

        private static void DisbandLost()
        {
            Gone.Clear();
            foreach ((IntPtr key, Pack pack) in PackByOwner)
            {
                if (pack.Owner == null || Owners.All(owner => owner.Pointer != key))
                {
                    Gone.Add(key);
                }
            }

            foreach (IntPtr key in Gone)
            {
                // Elsewhere the host's kill reaches the copies.
                if (CoopAccess.IsWorldAuthority)
                {
                    foreach (Actor summon in PackByOwner[key].Summons)
                    {
                        ActorSpawnAccess.TryKill(summon);
                    }
                }

                PackByOwner.Remove(key);
            }
        }

        /// <summary>
        /// Gives each copy the host announced to the Silverback it was spawned next to.
        /// </summary>
        private static void Claim()
        {
            for (int i = Unclaimed.Count - 1; i >= 0; i--)
            {
                (Actor copy, float since) = Unclaimed[i];
                if (copy == null || Time.time - since > ClaimSeconds)
                {
                    Unclaimed.RemoveAt(i);
                    continue;
                }

                Vector2 at = copy.GetFeetPosition();
                Pack? nearest = PackByOwner.Values
                    .Where(pack => pack.Owner != null && Vector2.Distance(pack.Owner.GetFeetPosition(), at) <= CopyReach)
                    .OrderBy(pack => Vector2.Distance(pack.Owner.GetFeetPosition(), at))
                    .FirstOrDefault();
                if (nearest != null)
                {
                    Enlist(nearest, copy);
                    Unclaimed.RemoveAt(i);
                }
            }
        }

        private static void Enlist(Pack pack, Actor summon)
        {
            // What the game's own conjure spells do to a summon.
            DeathBhvr_LootInventory loot = summon.GetComponentInChildren<DeathBhvr_LootInventory>();
            if (loot != null)
            {
                loot.enabled = false;
            }

            if (_kind == null)
            {
                _kind = Object.Instantiate(summon.GetEntityInfo());
                _kind.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            new EntityInfoModule(_kind).Apply(new ModuleContext(summon.gameObject, null));
            AlignmentContainer? neutral = BaseAlignmentContainer();
            if (neutral != null)
            {
                summon.AlignmentModule.SetBaseAlignment(neutral);
            }

            summon.CustomStats.IsFromSummoner = true;
            summon.GetStats().SetExpOnDead(0);
            pack.Summons.Add(summon);
            pack.PlayerKindsKey = 0;
            MelonLogger.Msg($"[Morph] {summon.name} joins the {pack.Owner.name}");
        }

        /// <summary>
        /// Makes the pack an enemy of whatever its Silverback is an enemy of, never of a player. Renewed when a player's body changes.
        /// </summary>
        private static void Swear(Pack pack)
        {
            if (pack.PlayerKindsKey == _playerKindsKey)
            {
                return;
            }

            // The Silverback's own kind counts every human body as an enemy, teammates included.
            IAlignment loyalty = new FriendAlignment(PlayerKinds, new SummonAlignment(pack.Owner).Cast<IAlignment>()).Cast<IAlignment>();
            foreach (Actor summon in pack.Summons)
            {
                if (summon == null)
                {
                    continue;
                }

                if (pack.Loyalty != null)
                {
                    summon.AlignmentModule.RemoveAlignment(pack.Loyalty);
                }

                summon.AlignmentModule.AddTempAlignment(loyalty);
            }

            pack.Loyalty = loyalty;
            pack.PlayerKindsKey = _playerKindsKey;
        }

        private static void Follow(Pack pack)
        {
            Il2CppCollections.HashSet<string> targets = pack.Owner.AIModule.GetTargetsInCombat();
            Vector2 feet = pack.Owner.GetFeetPosition();
            foreach (Actor summon in pack.Summons)
            {
                if (summon == null || summon.GetHealth().IsDead)
                {
                    continue;
                }

                if (targets.Count > 0)
                {
                    summon.AIModule.AddTargetsInCombat(targets);
                    summon.AIModule.SetInCombat(true, false);
                }
                else if (!summon.AIModule.IsInCombat() && Vector2.Distance(summon.GetFeetPosition(), feet) > LeashDistance)
                {
                    // ponytail: teleports instead of walking, a move-to order if the pop-in shows on screen.
                    summon.SetPos(NextTo(pack.Owner));
                }
            }
        }

        private static AlignmentContainer? BaseAlignmentContainer()
        {
            if (_baseAlignment == null)
            {
                _baseAlignment = Resources.FindObjectsOfTypeAll<AlignmentContainer>().FirstOrDefault(container => container.name == BaseAlignment);
            }

            return _baseAlignment;
        }

        private static Vector2 NextTo(Actor body)
        {
            return body.GetFeetPosition() + Random.insideUnitCircle.normalized * SpawnRadius;
        }

        /// <summary>
        /// One Silverback's summons and the alignment that binds them to it.
        /// </summary>
        private sealed class Pack(Actor owner)
        {
            internal Actor Owner { get; } = owner;

            internal List<Actor> Summons { get; } = [];

            internal IAlignment? Loyalty { get; set; }

            /// <summary>
            /// The player bodies <see cref="Loyalty"/> was made for. Zero makes it renew.
            /// </summary>
            internal long PlayerKindsKey { get; set; }
        }
    }
}