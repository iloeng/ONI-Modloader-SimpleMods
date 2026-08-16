using Common;
using Config;
using PeterHan.PLib.Options;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomizeRecipe
{
    [ConfigFile("CustomizeRecipe.json", true, true, typeof(Config.TranslationResolver))]
    [RestartRequired]
    public class CustomizeRecipeState : BaseSettings<CustomizeRecipeState>, IManualConfig
    {
        public override int Version { get; set; } = 3;

        [Option("CustomizeRecipe.LOCSTRINGS.LoadAllRecipesToConfig_Title", "CustomizeRecipe.LOCSTRINGS.LoadAllRecipesToConfig_ToolTip", "", null)]
        [JsonIgnore]
        public System.Action<object> LoadAllRecipesToConfig => delegate (object nix)
        {
            RecipePatch.Print();
            TrySave();

            OptionsDialog.Last?.CloseDialog();
            OptionsDialog.Last = null;
        };

        /// <summary>Whenever to force remove buildings 'store' flag. May have unexpected consequences.</summary>
        public bool OverrideStoreProduced { get; set; } = false;

        public bool CheatFast { get; set; } = false;

        public bool CheatFree { get; set; } = false;

        public bool AllowZeroInput { get; set; } = false;

        public List<RecipeData> RecipeSettings { get; set; } = new List<RecipeData>() {
            new RecipeData("RockCrusher_I_Fossil_O_Lime_SedimentaryRock", RockCrusherConfig.ID)
                .In(SimHashes.Fossil, 100f)
                .Out(SimHashes.Lime, 20f).Out(SimHashes.SedimentaryRock, 80f),
            new RecipeData("RockCrusher_NewExampleRecipe1", RockCrusherConfig.ID, Time: 5f, Description: "This is the description.")
                .In(SimHashes.Water, 100f).In(SimHashes.Dirt, 50f)
                .Out(SimHashes.Lime, 25f).Out(SimHashes.SedimentaryRock, 10f),
            new RecipeData(null, RockCrusherConfig.ID, Time: 40f, Description: "Turns Regolith into Sand and Dirt.")
                .In(SimHashes.Regolith, 100f)
                .Out(SimHashes.Sand, 100f),
            new RecipeData(null, GlassForgeConfig.ID, Time: 40f, Description: "Extracts pure <link=\"LIQUIDPHOSPHORUS\">Phosphorus</link> from <link=\"PHOSPHORITE\">Phosphorite</link>.")
                .In(SimHashes.Phosphorite, 100f)
                .Out(SimHashes.LiquidPhosphorus, 100f, ComplexRecipe.RecipeElement.TemperatureOperation.Melted)
        };

        #region _implementation

        public override string DefaultPath => Path.Combine(Util.RootFolder(), "mods", $"{FumiKMod.ModName}.json");

        protected override string BeforeUpdate(int oldVersion, string json)
        {
            if (oldVersion < 3)
            {
                json = json.Replace("\"version\"", "\"Version\"");
            }
            return json;
        }

        protected override bool OnUpdate()
        {
            if (Version < 2)
            {
                var rs = RecipeSettings;
                for (int i = rs.Count - 1; i >= 0; i--)
                {
                    if (rs[i].Inputs.Any(a => a.material is "0"))
                        rs.RemoveAt(i);
                }
            }
            return true;
        }

        protected override bool OnError(Exception e)
        {
            var text = e.ToString();
            Helpers.Print(text);
            if (text.Length > 1000)
                text = text.Substring(0, 1000);
            PostBootDialog.ToDialog(text);
            return false;
        }

        public object ReadSettings()
        {
            return Instance;
        }

        public void WriteSettings(object settings)
        {
            if (settings is CustomizeRecipeState state)
                state.TrySave();
            else
                TrySave();
        }

        public string GetConfigPath()
        {
            return DefaultPath;
        }

        public void OnLoaded()
        {
            // check if any recipe actually has zero inputs, if not disable the patch
            if (AllowZeroInput)
            {
                foreach (var recipe in RecipeSettings)
                {
                    foreach (var input in recipe.Inputs)
                    {
                        if (input.amount <= 0f || input.amounts != null && input.amounts.Any(a => a <= 0f))
                            goto exit_1;
                    }
                }
                AllowZeroInput = false;
            exit_1:;
            }
        }

        #endregion
    }

    public class CustomStrings
    {
        public static void LoadStrings()
        {
            #region 
            Helpers.StringsAddProperty("CustomizeRecipe.PROPERTY.version", "version");

            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.LoadAllRecipesToConfig_Title", "Load All Recipes To Config");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.LoadAllRecipesToConfig_ToolTip", "This will look up the current recipes and dump them into the manual config file. It's not needed to use this mod! While this is very verbose, it makes it easier to find existing recipes.");

            Helpers.StringsAddProperty("CustomizeRecipe.PROPERTY.CheatFast", "CheatFast");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.CheatFast_Title", "Cheat Fast");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.CheatFast_ToolTip", "If true, all recipes will be speeded up.");

            Helpers.StringsAddProperty("CustomizeRecipe.PROPERTY.CheatFree", "CheatFree");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.CheatFree_Title", "Cheat Free");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.CheatFree_ToolTip", "If true, all recipes need no materials.");

            Helpers.StringsAddProperty("CustomizeRecipe.PROPERTY.AllowZeroInput", "AllowZeroInput");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.AllowZeroInput_Title", "Allow Zero Input");
            Helpers.StringsAdd("CustomizeRecipe.LOCSTRINGS.AllowZeroInput_ToolTip", "Set this to true, if you plan to set ingredients to 0kg. This will prevent the game from crashing.");

            Helpers.StringsAddProperty("CustomizeRecipe.PROPERTY.RecipeSettings", "RecipeSettings");
            #endregion
        }
    }
}
