/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Healing", "VisEntities", "1.1.0")]
    [Description("Change how food and medical items affect players.")]
    public class BetterHealing : RustPlugin
    {
        #region Fields

        private static BetterHealing _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Items")]
            public Dictionary<string, ConsumableAttributes> Items { get; set; }
        }

        private class ConsumableAttributes
        {
            [JsonProperty("Instant Health")]
            public float InstantHealth { get; set; }

            [JsonProperty("Health Over Time")]
            public float HealthOverTime { get; set; }

            [JsonProperty("Calories")]
            public float Calories { get; set; }

            [JsonProperty("Hydration")]
            public float Hydration { get; set; }

            [JsonProperty("Poison (positive = adds, negative = removes)")]
            public float Poison { get; set; }

            [JsonProperty("Radiation (positive = adds, negative = removes)")]
            public float Radiation { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                Items = new Dictionary<string, ConsumableAttributes>
                {
                    ["bandage"] = new ConsumableAttributes
                    {
                        InstantHealth = 5f,
                        HealthOverTime = 0f,
                        Calories = 0f,
                        Hydration = 0f,
                        Poison = -2f,
                        Radiation = 0f
                    },
                    ["syringe.medical"] = new ConsumableAttributes
                    {
                        InstantHealth = 15f,
                        HealthOverTime = 20f,
                        Calories = 0f,
                        Hydration = 0f,
                        Poison = -5f,
                        Radiation = -10f
                    },
                    ["largemedkit"] = new ConsumableAttributes
                    {
                        InstantHealth = 0f,
                        HealthOverTime = 100f,
                        Calories = 0f,
                        Hydration = 0f,
                        Poison = -10f,
                        Radiation = 0f
                    },
                    ["pumpkin"] = new ConsumableAttributes
                    {
                        InstantHealth = 0f,
                        HealthOverTime = 10f,
                        Calories = 100f,
                        Hydration = 30f,
                        Poison = 0f,
                        Radiation = 0f
                    },
                    ["corn"] = new ConsumableAttributes
                    {
                        InstantHealth = 0f,
                        HealthOverTime = 6f,
                        Calories = 75f,
                        Hydration = 10f,
                        Poison = 0f,
                        Radiation = 0f
                    },
                    ["mushroom"] = new ConsumableAttributes
                    {
                        InstantHealth = 3f,
                        HealthOverTime = 0f,
                        Calories = 15f,
                        Hydration = 5f,
                        Poison = 0f,
                        Radiation = 0f
                    },
                    ["apple"] = new ConsumableAttributes
                    {
                        InstantHealth = 2f,
                        HealthOverTime = 0f,
                        Calories = 30f,
                        Hydration = 15f,
                        Poison = 0f,
                        Radiation = 0f
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object OnHealingItemUse(MedicalTool medicalTool, BasePlayer player)
        {
            if (player == null || !PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return null;

            Item item = medicalTool.GetItem();
            if (item == null)
                return null;

            ItemDefinition definition = item.info;
            if (definition == null)
                return null;

            string shortName = definition.shortname;
            if (shortName == null)
                return null;

            ConsumableAttributes attributes;
            bool hasStats = _config.Items.TryGetValue(shortName, out attributes);
            if (!hasStats)
                return null;

            ApplyItemEffects(player, attributes);
            return true;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action != "consume")
                return null;

            if (player == null || item == null || !PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return null;

            string shortName = item.info.shortname;
            if (shortName == null)
                return null;

            ConsumableAttributes attributes;
            bool hasStats = _config.Items.TryGetValue(shortName, out attributes);
            if (!hasStats)
                return null;

            item.UseItem(1);
            ApplyItemEffects(player, attributes);
            return true;
        }

        #endregion Oxide Hooks

        #region Core
        
        private static void ApplyItemEffects(BasePlayer player, ConsumableAttributes attributes)
        {
            if (attributes.InstantHealth != 0f)
            {
                float newHealth = player.health + attributes.InstantHealth;
                player.health = Mathf.Clamp(newHealth, 0f, player.MaxHealth());
            }

            if (attributes.HealthOverTime != 0f)
            {
                player.metabolism.ApplyChange(MetabolismAttribute.Type.HealthOverTime, attributes.HealthOverTime, 1f);
            }

            if (attributes.Calories != 0f)
            {
                if (attributes.Calories > 0f)
                {
                    player.metabolism.calories.Add(attributes.Calories);
                }
                else
                {
                    player.metabolism.calories.Subtract(Mathf.Abs(attributes.Calories));
                }
            }

            if (attributes.Hydration != 0f)
            {
                if (attributes.Hydration > 0f)
                {
                    player.metabolism.hydration.Add(attributes.Hydration);
                }
                else
                {
                    player.metabolism.hydration.Subtract(Mathf.Abs(attributes.Hydration));
                }
            }

            if (attributes.Poison != 0f)
            {
                if (attributes.Poison > 0f)
                {
                    player.metabolism.poison.Add(attributes.Poison);
                }
                else
                {
                    player.metabolism.poison.Subtract(Mathf.Abs(attributes.Poison));
                }
            }

            if (attributes.Radiation != 0f)
            {
                if (attributes.Radiation > 0f)
                {
                    player.metabolism.radiation_poison.Add(attributes.Radiation);
                }
                else
                {
                    player.metabolism.radiation_poison.Subtract(Mathf.Abs(attributes.Radiation));
                }
            }
        }

        #endregion Core

        #region Permissions

        public static class PermissionUtil
        {
            public const string USE = "betterhealing.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}