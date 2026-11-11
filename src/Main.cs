using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GTA;
using GTA.UI;
using LemonUI;
using LemonUI.Menus;
using TropicalMod.Core;
using TropicalMod.Modules;

namespace TropicalMod
{
    /// <summary>
    /// Ponto de entrada. O ScriptHookVDotNet instancia esta classe automaticamente.
    /// F5 abre o menu para ligar/desligar módulos.
    /// </summary>
    public class Main : Script
    {
        private const Keys MenuKey = Keys.F5;

        private readonly List<ModuleBase> _modules = new List<ModuleBase>
        {
            new DebugHudModule(),
            new Modules.SocialFeed.SocialFeedModule(),
            new Modules.Partner.PartnerModule(),
            // novos módulos aqui (um por dia de devlog)
        };

        private readonly ObjectPool _pool = new ObjectPool();
        private readonly NativeMenu _menu;

        public Main()
        {
            _menu = new NativeMenu("TropicalMod", "Módulos");
            _pool.Add(_menu);

            foreach (var module in _modules)
            {
                var mod = module;
                var item = new NativeCheckboxItem(mod.Name, mod.Description, mod.Enabled);
                item.CheckboxChanged += (s, e) => Safe(mod, "toggle", () => mod.SetEnabled(item.Checked));
                _menu.Add(item);
            }

            Tick += OnTick;
            KeyDown += OnKeyDown;
            Aborted += OnAborted;

            Log.Info("TropicalMod carregado.");
        }

        private bool _announced;

        private void OnTick(object sender, EventArgs e)
        {
            _pool.Process();

            if (!_announced && Game.Player.Character.Exists())
            {
                _announced = true;
                Notification.Show("~g~TropicalMod~s~ carregado. ~b~F5~s~ abre o menu.");
            }

            foreach (var module in _modules)
            {
                if (!module.Started)
                {
                    module.Started = true;
                    Safe(module, "OnStart", module.OnStart);
                }
                if (module.Enabled)
                    Safe(module, "OnTick", module.OnTick);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == MenuKey)
            {
                _menu.Visible = !_menu.Visible;
                return;
            }
            if (_menu.Visible) return;

            foreach (var module in _modules)
            {
                if (module.Enabled)
                    Safe(module, "OnKeyDown", () => module.OnKeyDown(e));
            }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            foreach (var module in _modules)
                Safe(module, "OnStop", module.OnStop);
        }

        /// <summary>Um módulo com bug é desligado em vez de derrubar o mod inteiro.</summary>
        private static void Safe(ModuleBase module, string stage, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"Módulo '{module.Name}' falhou em {stage}; desligando.", ex);
                Notification.Show($"~r~TropicalMod:~s~ módulo {module.Name} falhou (ver TropicalMod.log)");
                try { module.SetEnabled(false); } catch { /* ignora */ }
            }
        }
    }
}
