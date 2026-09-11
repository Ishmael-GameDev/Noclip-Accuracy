using System.Collections.Generic;
using Satchel.BetterMenus;
using UnityEngine;

namespace NoclipAccuracy
{
    public static class ConfigurationScreen
    {
        private static Menu _menuRef;

        private static string[] BuildSteps(float min, float max, float step)
        {
            List<string> vals = new();
            for (float v = min; v <= max + 0.0001f; v += step)
                vals.Add(v.ToString("0.0"));
            return vals.ToArray();
        }

        public static MenuScreen GetScreen(MenuScreen modListMenu, GlobalSettings settings)
        {
            string[] delayOptions = BuildSteps(0.1f, 0.5f, 0.1f);

            var elements = new List<Element>
            {
                new TextPanel("Noclip Accuracy"),

                new HorizontalOption(
                    "Show Counter",
                    "",
                    new[] { "On", "Off" },
                    i =>
                    {
                        settings.ShowCounter = i == 0;
                        NoclipAccuracy.Instance.Redraw();
                    },
                    () => settings.ShowCounter ? 0 : 1
                ),

                new HorizontalOption(
                    "Do Flash",
                    "",
                    new[] { "On", "Off" },
                    i =>
                    {
                        settings.DoFlash = i == 0;
                        NoclipAccuracy.Instance.Redraw();
                    },
                    () => settings.DoFlash ? 0 : 1
                ),

                new HorizontalOption(
                    "HUD Position",
                    "",
                    new[] { "Right", "Mid Right", "Low Right" },
                    i =>
                    {
                        settings.PositionIndex = i;
                        NoclipAccuracy.Instance.Redraw();
                    },
                    () => settings.PositionIndex
                ),

                new HorizontalOption(
                    "Burst Delay",
                    "Time before next hit registers",
                    delayOptions,
                    i =>
                    {
                        settings.BurstDelay = float.Parse(delayOptions[i]);
                    },
                    () => Mathf.RoundToInt(settings.BurstDelay * 10f)
                ),

                new KeyBind(
                    "Reset Counter",
                    settings.Keybinds.Reset
                )
            };

            _menuRef ??= new Menu(
                "Noclip Accuracy",
                elements.ToArray()
            );

            return _menuRef.GetMenuScreen(modListMenu);
        }
    }
}
