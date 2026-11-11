using System;
using System.Drawing;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using TropicalMod.Core;
using Font = GTA.UI.Font;

namespace TropicalMod.Modules.Partner
{
    /// <summary>
    /// Dia 2 — Parceiro de crime.
    /// F6 recruta o NPC mais próximo (ou chama um, se não houver ninguém); F6 de novo dispensa.
    /// Lealdade sobe com crimes juntos e fugas; cai se você atirar nele ou abandoná-lo.
    /// Com lealdade baixa ele pode trair: partir pra cima de você ou te dedurar pra polícia.
    /// Shift+F6 = força lealdade baixa (atalho de gravação).
    /// </summary>
    public class PartnerModule : ModuleBase
    {
        public override string Name => "Parceiro de crime";
        public override string Description => "F6 recruta/dispensa. Cuidado com a lealdade.";

        private const Keys RecruitKey = Keys.F6;
        private const float RecruitRadius = 6f;
        private const float AbandonDistance = 120f;
        private const int AbandonLeaveMs = 45000;
        private const int BetrayalThreshold = 20;
        private const int BetrayalCheckMs = 20000;
        private const int BetrayalChancePercent = 35;
        private const int ChatterCooldownMs = 6000;

        private static readonly string[] SpawnModels =
        {
            "g_f_y_vagos_01", "g_m_y_mexgoon_01", "a_f_y_hipster_02", "a_m_y_beach_01",
            "g_m_y_ballaorig_01", "a_f_y_beach_01", "a_m_y_stbla_02", "a_f_y_tourist_01",
        };

        private readonly Random _rng = new Random();
        private readonly TextElement _hud = new TextElement("", new PointF(640f, 8f), 0.4f, Color.White,
            Font.ChaletLondon, Alignment.Center) { Outline = true };

        private Ped _partner;
        private Blip _blip;
        private string _name = "";
        private int _loyalty;

        private int _lastWanted;
        private int _maxWanted;
        private int _farSince = -1;
        private int _nextLoyaltyTickAt;
        private int _nextBetrayalCheckAt;
        private int _nextChatterAt;
        private int _nextKillScanAt;
        private int _betrayAt = -1;
        private readonly System.Collections.Generic.HashSet<int> _handledDead = new System.Collections.Generic.HashSet<int>();

        private bool HasPartner => _partner != null && _partner.Exists();

        // ------------------------------------------------------------------ ciclo de vida

        public override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode != RecruitKey) return;

            if (e.Shift)
            {
                if (HasPartner) { _loyalty = 10; _nextBetrayalCheckAt = Game.GameTime; Notification.Show("~o~Debug:~s~ lealdade = 10"); }
                return;
            }

            if (HasPartner) Dismiss(); else Recruit();
        }

        public override void OnDisable() => Release(fleeing: false);
        public override void OnStop() => Release(fleeing: false);

        public override void OnTick()
        {
            if (!HasPartner)
            {
                _partner = null;
                return;
            }

            int now = Game.GameTime;
            Ped me = Game.Player.Character;

            if (_partner.IsDead)
            {
                Notification.Show($"~r~{_name} morreu.~s~ F6 para recrutar outro parceiro.");
                Release(fleeing: false);
                return;
            }

            if (_betrayAt >= 0)
            {
                if (now >= _betrayAt) Betray(me);
                DrawHud();
                return;
            }

            if (me.IsDead) { DrawHud(); return; }

            TrackWanted(now);
            TrackFriendlyFire(me);
            TrackDistance(me, now);
            if (!HasPartner) return; // foi embora por abandono
            if (now >= _nextKillScanAt) { _nextKillScanAt = now + 300; TrackPartnerKills(me); }
            TrackBetrayal(now);

            DrawHud();
        }

        // ------------------------------------------------------------------ recrutar / dispensar

        private void Recruit()
        {
            Ped me = Game.Player.Character;
            Ped target = FindRecruitable(me);
            bool spawned = false;

            if (target == null)
            {
                target = SpawnPartner(me);
                spawned = true;
                if (target == null)
                {
                    Notification.Show("~r~Não consegui chamar um parceiro agora.");
                    return;
                }
            }

            _partner = target;
            _partner.IsPersistent = true;
            Function.Call(Hash.CLEAR_PED_TASKS, _partner);

            int group = Function.Call<int>(Hash.GET_PLAYER_GROUP, Game.Player);
            Function.Call(Hash.SET_PED_AS_GROUP_MEMBER, _partner, group);
            Function.Call(Hash.SET_PED_NEVER_LEAVES_GROUP, _partner, true);
            int myRel = Function.Call<int>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, me);
            Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, _partner, myRel);

            // não foge, luta junto
            Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, _partner, 0, false);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _partner, 46, true); // sempre luta
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, _partner, 5, true);  // luta armado contra armado
            _partner.Weapons.Give(WeaponHash.Pistol, 250, true, true);
            _partner.Weapons.Give(WeaponHash.MicroSMG, 300, false, true);
            _partner.Armor = 100;
            _partner.Accuracy = 45;

            _blip = _partner.AddBlip();
            _blip.Color = BlipColor.Green;
            _blip.Name = "Parceiro";

            bool female = _partner.Gender == Gender.Female;
            _name = PartnerTexts.Pick(_rng, female ? PartnerTexts.FemaleNames : PartnerTexts.MaleNames);
            _loyalty = 60;
            _lastWanted = Game.Player.WantedLevel;
            _maxWanted = _lastWanted;
            _farSince = -1;
            _betrayAt = -1;
            _nextBetrayalCheckAt = Game.GameTime + BetrayalCheckMs;
            _handledDead.Clear();
            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, _partner);

            Notification.Show($"~g~{_name}~s~ {(spawned ? "chegou" : "topou")} ser seu parceiro{(female ? "a" : "")}.");
            Say(PartnerTexts.Pick(_rng, PartnerTexts.Recruited), force: true);
            Log.Info($"Parceiro recrutado: {_name} (spawned={spawned})");
        }

        private Ped FindRecruitable(Ped me)
        {
            Ped best = null;
            float bestDist = RecruitRadius;
            foreach (Ped p in World.GetNearbyPeds(me, RecruitRadius))
            {
                if (p == null || !p.Exists() || p.IsDead || !p.IsHuman || p.Handle == me.Handle) continue;
                if (IsCop(p) || Function.Call<bool>(Hash.IS_PED_A_PLAYER, p)) continue;
                if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, p)) continue;

                float d = p.Position.DistanceTo(me.Position);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        private Ped SpawnPartner(Ped me)
        {
            var model = new Model(SpawnModels[_rng.Next(SpawnModels.Length)]);
            if (!model.Request(1500)) return null;

            Vector3 pos = me.Position + me.RightVector * 2f - me.ForwardVector * 1.5f;
            Ped p = World.CreatePed(model, pos, me.Heading);
            model.MarkAsNoLongerNeeded();
            return p != null && p.Exists() ? p : null;
        }

        private void Dismiss()
        {
            Say(PartnerTexts.Pick(_rng, PartnerTexts.Dismissed), force: true);
            Notification.Show($"Você dispensou ~g~{_name}~s~.");
            Release(fleeing: false);
        }

        /// <summary>Solta o parceiro de volta pro mundo (sem deletar: ele vira NPC comum).</summary>
        private void Release(bool fleeing)
        {
            if (_blip != null && _blip.Exists()) _blip.Delete();
            _blip = null;

            if (HasPartner)
            {
                Function.Call(Hash.REMOVE_PED_FROM_GROUP, _partner);
                if (!fleeing && !_partner.IsDead)
                {
                    Function.Call(Hash.CLEAR_PED_TASKS, _partner);
                    Function.Call(Hash.TASK_WANDER_STANDARD, _partner, 10f, 10);
                }
                _partner.MarkAsNoLongerNeeded();
            }
            _partner = null;
            _betrayAt = -1;
        }

        // ------------------------------------------------------------------ lealdade

        private void TrackWanted(int now)
        {
            int wanted = Game.Player.WantedLevel;
            if (wanted > _lastWanted)
            {
                _maxWanted = Math.Max(_maxWanted, wanted);
                AddLoyalty(+3);
                Say(PartnerTexts.Pick(_rng, PartnerTexts.WantedUp));
            }
            else if (wanted == 0 && _lastWanted > 0 && !_partner.IsDead)
            {
                AddLoyalty(+5 + _maxWanted * 4); // fugir de 5 estrelas juntos = +25
                Say(PartnerTexts.Pick(_rng, PartnerTexts.Escaped), force: true);
                _maxWanted = 0;
            }
            _lastWanted = wanted;
        }

        private void TrackFriendlyFire(Ped me)
        {
            bool hitByMe = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, _partner, me, true);
            Vehicle myVeh = me.IsInVehicle() ? me.CurrentVehicle : null;
            if (!hitByMe && myVeh != null)
                hitByMe = Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, _partner, myVeh, true);

            if (!hitByMe) return;

            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, _partner);
            AddLoyalty(-15);
            Say(PartnerTexts.Pick(_rng, PartnerTexts.HurtByPlayer), force: true);
        }

        private void TrackDistance(Ped me, int now)
        {
            float dist = _partner.Position.DistanceTo(me.Position);
            if (dist < AbandonDistance)
            {
                _farSince = -1;
                return;
            }

            if (_farSince < 0)
            {
                _farSince = now;
                Say(PartnerTexts.Pick(_rng, PartnerTexts.Abandoned), force: true);
            }

            if (now >= _nextLoyaltyTickAt)
            {
                _nextLoyaltyTickAt = now + 3000;
                AddLoyalty(-1);
            }

            if (now - _farSince > AbandonLeaveMs)
            {
                Notification.Show($"~g~{_name}~s~ cansou de esperar e foi embora.");
                Release(fleeing: false);
            }
        }

        private void TrackPartnerKills(Ped me)
        {
            if (_handledDead.Count > 500) _handledDead.Clear();
            foreach (Ped p in World.GetNearbyPeds(_partner, 60f))
            {
                if (p == null || !p.Exists() || !p.IsDead || p.Handle == me.Handle) continue;
                if (!_handledDead.Add(p.Handle)) continue;

                int source = Function.Call<int>(Hash.GET_PED_SOURCE_OF_DEATH, p);
                if (source != _partner.Handle) continue;

                AddLoyalty(+2);
                Say(PartnerTexts.Pick(_rng, PartnerTexts.PartnerKill));
            }
        }

        private void AddLoyalty(int delta)
        {
            int before = _loyalty;
            _loyalty = Math.Max(0, Math.Min(100, _loyalty + delta));

            if (before > 35 && _loyalty <= 35)
                Say(PartnerTexts.Pick(_rng, PartnerTexts.Warning), force: true);
        }

        // ------------------------------------------------------------------ traição

        private void TrackBetrayal(int now)
        {
            if (now < _nextBetrayalCheckAt) return;
            _nextBetrayalCheckAt = now + BetrayalCheckMs;

            if (_loyalty > BetrayalThreshold) return;
            if (_rng.Next(100) >= BetrayalChancePercent) return;

            // anuncia, espera 2.5s e trai: dá tempo de reagir (e de gravar)
            Say(PartnerTexts.Pick(_rng, PartnerTexts.BetrayalWindup), force: true, ms: 3000);
            _betrayAt = now + 2500;
        }

        private void Betray(Ped me)
        {
            _betrayAt = -1;
            Ped traitor = _partner;
            string name = _name;
            bool snitch = _rng.Next(2) == 0 || traitor.IsInVehicle();

            Function.Call(Hash.REMOVE_PED_FROM_GROUP, traitor);
            Function.Call(Hash.SET_PED_NEVER_LEAVES_GROUP, traitor, false);
            if (_blip != null && _blip.Exists()) _blip.Color = BlipColor.Red;

            if (snitch)
            {
                // dedurou: estrelas pra você, ele sai correndo
                Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 3);
                if (traitor.IsInVehicle()) Function.Call(Hash.TASK_LEAVE_ANY_VEHICLE, traitor, 0, 256);
                Function.Call(Hash.TASK_SMART_FLEE_PED, traitor, me, 500f, -1, false, false);
                Notification.Show($"~r~{name} te dedurou pra polícia!");
            }
            else
            {
                // partiu pra cima
                int hates = Function.Call<int>(Hash.GET_HASH_KEY, "HATES_PLAYER");
                Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_HASH, traitor, hates);
                Function.Call(Hash.TASK_COMBAT_PED, traitor, me, 0, 16);
                Notification.Show($"~r~{name} te traiu!");
            }

            Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "LOSER", "HUD_AWARDS", true);
            Log.Info($"Traição: {name} ({(snitch ? "dedurou" : "atacou")})");

            // ele continua no mundo como inimigo/fugitivo; o módulo para de controlar
            if (_blip != null && _blip.Exists()) _blip.Delete();
            _blip = null;
            traitor.AddBlip().Color = BlipColor.Red;
            traitor.MarkAsNoLongerNeeded();
            _partner = null;
        }

        // ------------------------------------------------------------------ util

        private void Say(string line, bool force = false, int ms = 2500)
        {
            int now = Game.GameTime;
            if (!force && now < _nextChatterAt) return;
            _nextChatterAt = now + ChatterCooldownMs;
            GTA.UI.Screen.ShowSubtitle($"~g~{_name}:~s~ {line}", ms);
        }

        private void DrawHud()
        {
            if (!HasPartner || Game.IsCutsceneActive || Game.IsPaused) return;
            string color = _loyalty > 60 ? "~g~" : _loyalty > BetrayalThreshold + 15 ? "~y~" : "~r~";
            _hud.Caption = $"{_name}  |  Lealdade {color}{_loyalty}%";
            _hud.Draw();
        }

        private static bool IsCop(Ped p)
        {
            int type = Function.Call<int>(Hash.GET_PED_TYPE, p);
            return type == 6 || type == 27 || type == 29;
        }
    }
}
