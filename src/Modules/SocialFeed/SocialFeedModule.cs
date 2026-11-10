using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using TropicalMod.Core;

namespace TropicalMod.Modules.SocialFeed
{
    /// <summary>
    /// Dia 9 — "Bafafá": seus crimes viram posts virais.
    /// Detecta mortes, roubo de carro, estrelas, fugas, velocidade e manobras; cada evento vira um post
    /// e rende seguidores. Testemunhas próximas sacam o celular e filmam.
    /// </summary>
    public class SocialFeedModule : ModuleBase
    {
        public override string Name => "Bafafá (rede social)";
        public override string Description => "Seus crimes viram posts e rendem seguidores.";

        // --- ajuste fino (bom pra mostrar no devlog) ---
        private const int MaxPostsOnScreen = 4;
        private const int PostLifetimeMs = 25000;
        private const int ComboWindowMs = 15000;
        private const int MaxCombo = 5;
        private const float WitnessRadius = 35f;
        private const int MaxWitnesses = 3;
        private const int FilmDurationMs = 10000;
        private const float SpeedingKmh = 160f;
        private const int SpeedingHoldMs = 3000;
        private const int AirHoldMs = 1500;

        private static readonly long[] Milestones = { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };

        private static readonly string SavePath =
            Path.Combine(Log.ModDir, "TropicalMod.followers.txt");

        private readonly Random _rng = new Random();
        private readonly SocialFeedHud _hud = new SocialFeedHud();
        private readonly List<FeedPost> _posts = new List<FeedPost>();
        private readonly HashSet<int> _handledDead = new HashSet<int>();
        private readonly Dictionary<int, (Ped ped, int until)> _filming = new Dictionary<int, (Ped, int)>();
        private readonly Dictionary<CrimeKind, int> _cooldownUntil = new Dictionary<CrimeKind, int>();

        private long _followers;
        private int _combo;
        private int _lastEventAt = -999999;

        private int _nextScanAt;
        private int _lastWanted;
        private int _maxWantedThisChase;
        private bool _wasJacking;
        private bool _wasDead;
        private bool _wasBusted;
        private int _lastFailAt = -999999;
        private int _speedingSince = -1;
        private int _airSince = -1;

        public override void OnStart()
        {
            _followers = LoadFollowers();
            Log.Info($"Bafafá: {_followers} seguidores carregados.");
        }

        public override void OnDisable() => StopAllFilming();

        public override void OnStop()
        {
            StopAllFilming();
            SaveFollowers();
        }

        public override void OnTick()
        {
            int now = Game.GameTime;
            Ped me = Game.Player.Character;
            if (me == null || !me.Exists()) return;

            DetectPlayerState(me, now);
            if (!me.IsDead)
            {
                DetectVehicleFeats(me, now);
                if (now >= _nextScanAt)
                {
                    _nextScanAt = now + 250;
                    DetectKills(me);
                }
            }

            UpdateFilming(now);
            UpdatePosts(now);

            if (_combo > 0 && now - _lastEventAt > ComboWindowMs) _combo = 0;

            // não poluir a tela em cutscene / mapa aberto
            if (!Game.IsCutsceneActive && !Game.IsPaused)
                _hud.Draw(_posts, _followers, _combo, now);
        }

        // ------------------------------------------------------------------ detecção

        private void DetectPlayerState(Ped me, int now)
        {
            int wanted = Game.Player.WantedLevel;

            // morte/prisão primeiro: elas também zeram as estrelas e não podem contar como "fuga"
            bool dead = me.IsDead;
            if (dead && !_wasDead)
            {
                Emit(CrimeKind.Wasted, wanted);
                _lastFailAt = now;
            }
            _wasDead = dead;

            bool busted = IsBusted();
            if (busted && !_wasBusted)
            {
                Emit(CrimeKind.Busted, wanted);
                _lastFailAt = now;
            }
            _wasBusted = busted;

            bool recentlyFailed = dead || busted || now - _lastFailAt < 15000;

            if (wanted > _lastWanted)
            {
                _maxWantedThisChase = Math.Max(_maxWantedThisChase, wanted);
                Emit(CrimeKind.WantedUp, wanted, cooldownMs: 3000);
            }
            else if (wanted == 0 && _lastWanted > 0)
            {
                if (!recentlyFailed) Emit(CrimeKind.Escaped, _maxWantedThisChase);
                _maxWantedThisChase = 0;
            }
            _lastWanted = wanted;

            bool jacking = Function.Call<bool>(Hash.IS_PED_JACKING, me);
            if (jacking && !_wasJacking) Emit(CrimeKind.CarJack, wanted, cooldownMs: 4000);
            _wasJacking = jacking;
        }

        private bool IsBusted() => Function.Call<bool>(Hash.IS_PLAYER_BEING_ARRESTED, Game.Player, true);

        private void DetectVehicleFeats(Ped me, int now)
        {
            Vehicle veh = me.IsInVehicle() ? me.CurrentVehicle : null;
            if (veh == null || veh.Driver != me)
            {
                _speedingSince = -1;
                _airSince = -1;
                return;
            }

            // velocidade sustentada
            float kmh = veh.Speed * 3.6f;
            if (kmh >= SpeedingKmh)
            {
                if (_speedingSince < 0) _speedingSince = now;
                else if (now - _speedingSince >= SpeedingHoldMs)
                {
                    Emit(CrimeKind.Speeding, Game.Player.WantedLevel, cooldownMs: 45000);
                    _speedingSince = -1;
                }
            }
            else _speedingSince = -1;

            // manobra: tempo no ar, conta ao pousar
            if (veh.IsInAir)
            {
                if (_airSince < 0) _airSince = now;
            }
            else if (_airSince >= 0)
            {
                if (now - _airSince >= AirHoldMs)
                    Emit(CrimeKind.Stunt, Game.Player.WantedLevel, cooldownMs: 8000);
                _airSince = -1;
            }
        }

        private void DetectKills(Ped me)
        {
            if (_handledDead.Count > 1000) _handledDead.Clear();

            Vehicle myVeh = me.IsInVehicle() ? me.CurrentVehicle : null;

            foreach (Ped p in World.GetNearbyPeds(me, 80f))
            {
                if (p == null || !p.Exists() || !p.IsDead || p.Handle == me.Handle) continue;
                if (!_handledDead.Add(p.Handle)) continue;

                int source = Function.Call<int>(Hash.GET_PED_SOURCE_OF_DEATH, p);
                bool byMe = source == me.Handle || (myVeh != null && source == myVeh.Handle);
                if (!byMe) continue;

                Emit(IsCop(p) ? CrimeKind.CopKill : CrimeKind.Kill, Game.Player.WantedLevel, cooldownMs: 1500);
            }
        }

        private static bool IsCop(Ped p)
        {
            int type = Function.Call<int>(Hash.GET_PED_TYPE, p);
            return type == 6 || type == 27 || type == 29; // cop, swat, army
        }

        // ------------------------------------------------------------------ eventos → posts

        private void Emit(CrimeKind kind, int stars, int cooldownMs = 0)
        {
            int now = Game.GameTime;
            if (_cooldownUntil.TryGetValue(kind, out int until) && now < until) return;
            if (cooldownMs > 0) _cooldownUntil[kind] = now + cooldownMs;

            bool negative = kind == CrimeKind.Wasted || kind == CrimeKind.Busted;
            long gain;

            if (negative)
            {
                gain = -(long)Math.Min(_followers * 0.05, 50_000); // perde 5% (máx 50 mil)
                _combo = 0;
            }
            else
            {
                _combo = now - _lastEventAt <= ComboWindowMs ? Math.Min(_combo + 1, MaxCombo) : 1;
                double mult = (1 + stars * 0.5) * _combo * (0.8 + _rng.NextDouble() * 0.4);
                gain = (long)(SocialFeedTexts.BaseFollowers(kind) * mult);
                MakeWitnessesFilm(now);
            }
            _lastEventAt = now;

            long before = _followers;
            _followers = Math.Max(0, _followers + gain);
            CheckMilestones(before, _followers);

            _posts.Insert(0, new FeedPost
            {
                Handle = SocialFeedTexts.RandomHandle(_rng),
                Text = SocialFeedTexts.RandomText(_rng, kind, Math.Max(stars, 1)),
                LikesTarget = Math.Max(12, Math.Abs(gain) * _rng.Next(3, 9)),
                Likes = 0,
                CreatedAt = now,
                Negative = negative,
            });
            if (_posts.Count > MaxPostsOnScreen) _posts.RemoveAt(_posts.Count - 1);

            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "Text_Arrive_Tone", "Phone_SoundSet_Default", true);
            SaveFollowers();
        }

        private void UpdatePosts(int now)
        {
            for (int i = _posts.Count - 1; i >= 0; i--)
            {
                var post = _posts[i];
                if (now - post.CreatedAt > PostLifetimeMs) { _posts.RemoveAt(i); continue; }

                // curtidas sobem rápido no começo e desaceleram (~4s)
                float t = Math.Min(1f, (now - post.CreatedAt) / 4000f);
                float eased = 1f - (1f - t) * (1f - t);
                post.Likes = (long)(post.LikesTarget * eased);
            }
        }

        private void CheckMilestones(long before, long after)
        {
            foreach (long m in Milestones)
            {
                if (before < m && after >= m)
                {
                    Notification.Show($"~p~{SocialFeedHud.AppName}~s~: você chegou a ~y~{SocialFeedTexts.FormatCount(m)}~s~ seguidores!");
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHECKPOINT_PERFECT", "HUD_MINI_GAME_SOUNDSET", true);
                }
            }
        }

        // ------------------------------------------------------------------ testemunhas filmando

        private void MakeWitnessesFilm(int now)
        {
            Ped me = Game.Player.Character;
            int added = 0;

            foreach (Ped p in World.GetNearbyPeds(me, WitnessRadius))
            {
                if (added >= MaxWitnesses) break;
                if (p == null || !p.Exists() || p.IsDead || !p.IsHuman || p.IsInVehicle()) continue;
                if (p.Handle == me.Handle || _filming.ContainsKey(p.Handle) || IsCop(p)) continue;
                if (Function.Call<bool>(Hash.IS_PED_IN_COMBAT, p, me)) continue;

                Vector3 d = me.Position - p.Position;
                p.Heading = (float)(Math.Atan2(-d.X, d.Y) * 180.0 / Math.PI);
                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, p, "WORLD_HUMAN_MOBILE_FILM_SHOCKING", 0, true);

                _filming[p.Handle] = (p, now + FilmDurationMs);
                added++;
            }
        }

        private void UpdateFilming(int now)
        {
            if (_filming.Count == 0) return;

            var done = new List<int>();
            foreach (var kv in _filming)
            {
                if (now < kv.Value.until) continue;
                Ped p = kv.Value.ped;
                if (p != null && p.Exists() && !p.IsDead) p.Task.ClearAll();
                done.Add(kv.Key);
            }
            foreach (int h in done) _filming.Remove(h);
        }

        private void StopAllFilming()
        {
            foreach (var kv in _filming)
            {
                Ped p = kv.Value.ped;
                if (p != null && p.Exists() && !p.IsDead) p.Task.ClearAll();
            }
            _filming.Clear();
        }

        // ------------------------------------------------------------------ persistência

        private static long LoadFollowers()
        {
            try
            {
                return File.Exists(SavePath) && long.TryParse(File.ReadAllText(SavePath).Trim(), out long n) ? n : 0;
            }
            catch (Exception ex)
            {
                Log.Error("Bafafá: falha ao ler seguidores", ex);
                return 0;
            }
        }

        private void SaveFollowers()
        {
            try { File.WriteAllText(SavePath, _followers.ToString()); }
            catch (Exception ex) { Log.Error("Bafafá: falha ao salvar seguidores", ex); }
        }
    }
}
