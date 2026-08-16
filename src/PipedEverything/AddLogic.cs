using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Common;
using Shared.CollectionNS;
using PeterHan.PLib.Core;
using Shared.JsonNS;

namespace PipedEverything
{
    public static class AddLogic
    {
        public static bool DidApplyModifications;

        public static void TryAddLogic(BuildingDef def)
        {
            if (def?.PrefabID == null)
                return;

            if (!DidApplyModifications)
            {
                DidApplyModifications = true;
                ApplyPostModifications();
            }

            foreach (var config in PipedEverythingState.Instance.Configs.Where(w => w.Id == def.PrefabID || w.Id == def.Name.StripLinks()))
            {
                if (config.OriginalPort is null)
                    TryAddLogicNew(def, config);
                else
                    TryAddLogicOriginal(def, config);
            }
        }

        private static void TryAddLogicNew(BuildingDef def, PipeConfig config)
        {
            var conduitType = ConduitType.None;
            var offset = new CellOffset(config.OffsetX, config.OffsetY);
            var color = config.Color;
            var filters = new List<SimHashes>();
            var filterTags = new List<Tag>();
            bool isToxic = false;
            Helpers.PrintDebug($"AddLogic adding {config.Id} {offset}");
            foreach (var filter in config.Filter)
            {
                if (filter == "Solid")
                {
                    conduitType = ConduitType.Solid;
                    filters.Add(SimHashes.Void);
                    continue;
                }
                if (filter == "Liquid")
                {
                    conduitType = ConduitType.Liquid;
                    filters.Add(SimHashes.Void);
                    continue;
                }
                if (filter == "Gas")
                {
                    conduitType = ConduitType.Gas;
                    filters.Add(SimHashes.Void);
                    continue;
                }

                filterTags.Add(filter.ToTagSafe());

                var element = filter.ToElement();
                if (element.id == 0)
                {
                    if (conduitType is ConduitType.None) // allow Tags only on solid filters
                        conduitType = ConduitType.Solid;
                    else if (conduitType is not ConduitType.Solid)
                    {
                        Helpers.PrintDialog($"Unable to resolve: {filter} in {config.Id}");
                        continue;
                    }
                    foreach (var v in filter.GetElements())
                    {
                        if (v.IsSolid)
                            filters.Add(v.id);
                    }
                    if (filters.Count == 0)
                        Helpers.PrintDialog($"Unable to resolve: {filter} in {config.Id}");
                    continue;
                }

                if (conduitType == ConduitType.None)
                    conduitType = element.IsGas ? ConduitType.Gas : element.IsLiquid ? ConduitType.Liquid : ConduitType.Solid;
                else if (conduitType != element.GetConduitType())
                {
                    Helpers.PrintDialog($"Element does not match conduit type {conduitType}: {filter} in {config.Id}");
                    continue;
                }

                if (element.sublimateId != 0)
                    isToxic = true;

                color ??= GetColor(element);
                filters.Add(element.id);
            }

            if (conduitType == ConduitType.None)
            {
                Helpers.PrintDialog($"No valid filter for {config.Id}");
                return;
            }

            // set default for outputs on ComplexFabricator to 3rd storage (usually)
            var complexFabricator = def.BuildingComplete.GetComponent<ComplexFabricator>();
            if (complexFabricator != null)
            {
                config.StorageIndex ??= def.BuildingComplete.GetComponents<Storage>().GetIndex(f => ReferenceEquals(f, config.Input ? complexFabricator.inStorage : complexFabricator.outStorage));
            }

            // check storage valid
            config.StorageIndex ??= 0;
            var storages = def.BuildingComplete.GetComponents<Storage>();
            if (config.StorageIndex < 0 || storages.Length <= config.StorageIndex)
            {
                Helpers.PrintDialog($"Storage index out of range for {config.Id}");
                return;
            }

            // attach controller
            var portInfo = new PortDisplayInfo(filters.ToArray(), filterTags.ToArray(), conduitType, offset, config.Input, color, config.ColorBackground, config.ColorBorder, config.StorageIndex, config.StorageCapacity);
            def.BuildingComplete.AddOrGet<PortDisplayController>().AddPort(def.BuildingComplete, portInfo);
            def.BuildingUnderConstruction.AddOrGet<PortDisplayController>().AddPort(def.BuildingUnderConstruction, portInfo);
            def.BuildingPreview.AddOrGet<PortDisplayController>().AddPort(def.BuildingPreview, portInfo);

            // add capacity and set sealed state
            var storage = storages[portInfo.StorageIndex];
            if (portInfo.StorageCapacity < float.MaxValue) // don't add ridiculous capacities
                storage.capacityKg += portInfo.StorageCapacity * portInfo.filters.Length;
            if (isToxic && !storage.defaultStoredItemModifers.Contains(Storage.StoredItemModifier.Seal))
                storage.defaultStoredItemModifers.Add(Storage.StoredItemModifier.Seal);

            // add conduit consumer/dispenser
            if (config.Input)
            {
                if (conduitType != ConduitType.Solid)
                    def.BuildingComplete.AddComponent<ConduitConsumerOptional>().AssignPort(portInfo);
                else
                    def.BuildingComplete.AddComponent<ConduitConsumerOptionalSolid>().AssignPort(portInfo);
            }
            else
            {
                if (conduitType != ConduitType.Solid)
                    def.BuildingComplete.AddComponent<ConduitDispenserOptional>().AssignPort(portInfo);
                else
                    def.BuildingComplete.AddComponent<ConduitDispenserOptionalSolid>().AssignPort(portInfo);
            }

            if (config.RemoveMaxAtmosphere == true)
            {
                var electrolyzer = def.BuildingComplete.GetComponent<Electrolyzer>();
                electrolyzer?.maxMass = 100000f;
                var rustDeoxidizer = def.BuildingComplete.GetComponent<RustDeoxidizer>();
                rustDeoxidizer?.maxMass = 100000f;
                var oilRefinery = def.BuildingComplete.GetComponent<OilRefinery>();
                if (oilRefinery != null)
                {
                    oilRefinery.overpressureMass = 100000f;
                    oilRefinery.overpressureWarningMass = 99000f;
                }
            }

            Helpers.Print($"Controller added port {config.Id} {conduitType} {offset} input={config.Input}");
        }

        private static void TryAddLogicOriginal(BuildingDef def, PipeConfig config)
        {
            if (config.OriginalPort is null)
                throw new ArgumentNullException(nameof(config.OriginalPort));

            if (config.OriginalPort is Port.Utility)
            {
                // option to remove conduit
                string filter = config.Filter.Length == 1 ? config.Filter[0] : "";
                if (filter == "DESTROY")
                {
                    if (config.Input)
                    {
                        def.InputConduitType = ConduitType.None;
                        def.BuildingComplete.RemoveComponent<ConduitConsumer>();
                    }
                    else
                    {
                        def.OutputConduitType = ConduitType.None;
                        def.BuildingComplete.RemoveComponent<ConduitDispenser>();
                    }
                }
                else
                {
                    // option to change element (for wrong-element damage)
                    if (config.Input && filter is not (null or ""))
                        def.BuildingComplete.GetComponent<ConduitConsumer>()?.capacityTag = filter.ToTag();

                    // change X, Y of Utility port
                    if (config.Input)
                        def.UtilityInputOffset = new(config.OffsetX, config.OffsetY);
                    else
                        def.UtilityOutputOffset = new(config.OffsetX, config.OffsetY);
                }
            }
            else
            {
                // change X, Y of Extra port
                int index = (int)config.OriginalPort;
                if (config.Input)
                {
                    var ports = def.BuildingComplete.GetComponents<ConduitSecondaryInput>();
                    if (ports.Length > index)
                        ports[index].portInfo.offset = new(config.OffsetX, config.OffsetY); // this will also mutated preview/construction
                }
                else
                {
                    var ports = def.BuildingComplete.GetComponents<ConduitSecondaryOutput>();
                    if (ports.Length > index)
                        ports[index].portInfo.offset = new(config.OffsetX, config.OffsetY); // this will also mutated preview/construction
                }
            }
        }

        public static void TryAddGeyser(GameObject go)
        {
            // get geyser data
            var configurator = go.GetComponent<GeyserConfigurator>();
            if (configurator == null)
                return;
            var config = GeyserConfigurator.FindType(configurator.presetType); //?? throw new NullReferenceException(nameof(GeyserConfigurator.GeyserType));
            if (config == null)
            {
                Helpers.Print($"Error: Geyser id='{go.GetComponent<KPrefabID>()?.PrefabTag}' type='{configurator.presetType}' is invalid");
                return;
            }
            var element = config.element.ToElement();
            if (element.id == SimHashes.Void)
                throw new Exception("Geyser element is Void");

            // add storage
            var storage = go.AddOrGet<Storage>();
            storage.capacityKg = element.IsGas ? PipedEverythingState.Instance.GeyserGasStorageKG : PipedEverythingState.Instance.GeyserStorageKG;
            storage.defaultStoredItemModifers = Storage.StandardInsulatedStorage;

            // add port
            var portInfo = new PortDisplayInfo(filters: [SimHashes.Void], filterTags: [element.GetConduitType().ToString()], type: element.GetConduitType(),
                offset: new(0, 1), input: false, color: GetColor(element), background: default, border: default, storageIndex: default, storageCapacity: float.PositiveInfinity);
            go.AddOrGet<PortDisplayController>().AddPort(go, portInfo);
            go.AddOrGet<ConduitDispenserGeyser>();

            Helpers.Print($"Attached pipe dispenser to {go.PrefabID()}");
        }

        public static Color32 GetColor(Element element)
        {
            Color32 color = element.substance.conduitColour;
            color.a = 255; // for some reason the alpha channel is set to invisible for some elements (hydrogen only?)

            if (color.r == 0 && color.g == 0 && color.b == 0)
            {
                // avoid completely black icons since the background is black
                color.r = 25;
                color.g = 25;
                color.b = 25;
            }
            return color;
        }

        public static void ApplyPostModifications()
        {
            if (PipedEverythingState.Instance.API_Blacklist.Contains("*"))
                return;
            var mods = PRegistry.GetData<List<string>>("PipedEverything.PostMod");
            if (mods == null)
                return;
            var state = PostModState.Skip;
            foreach (var mod in mods)
            {
                try
                {
                    if (mod.StartsWith("$"))
                    {
                        Helpers.Print($"Post modification request from {mod}");
                        if (PipedEverythingState.Instance.API_Blacklist.Contains(mod))
                            state = PostModState.Skip;
                        else
                            state = PostModState.None;
                    }
                    else if (state == PostModState.Skip)
                        continue;
                    else if (mod is "ADD")
                        state = PostModState.Add;
                    else if (mod is "DEL")
                        state = PostModState.Delete;
                    else
                    {
                        var config = JsonTool.Deserialize<PipeConfig>(mod) ?? throw new Exception("unable to deserialize");
                        switch (state)
                        {
                            case PostModState.Delete:
                                int count = PipedEverythingState.Instance.Configs.RemoveAll(a => a.Equals(config));
                                Helpers.Print($"Removed {count} pipes from {config.Id}");
                                break;
                            case PostModState.Add:
                                if (!PipedEverythingState.Instance.Configs.Contains(config))
                                    PipedEverythingState.Instance.Configs.Add(config);
                                Helpers.Print($"Added pipe to {config.Id}");
                                break;
                        }
                    }
                }
                catch (Exception)
                {
                    Helpers.Print($"PostMod invalid: {mod}");
                }
            }
        }

        public static void PRegistryExample()
        {
            // try to get list or, if it does exist yet, create a new list
            var mods = PRegistry.GetData<List<string>>("PipedEverything.PostMod");
            if (mods == null)
                PRegistry.PutData("PipedEverything.PostMod", mods = new());

            // every instruction set must start with '$' plus a unique name, usually the mods
            mods.Add("$MyMod");

            // next define which action should be done
            mods.Add("ADD");

            // then define the any number of entries as a json string; this is identical to the config file
            mods.Add("""{"Id":"RustDeoxidizer","Input":false,"OffsetX":1,"OffsetY":0,"Filter":["IronOre"]}""");
            mods.Add("""{"Id":"RustDeoxidizer","Input":false,"OffsetX":0,"OffsetY":1,"Filter":["IronOre"]}""");

            // when deleting entries, optional properties can be omitted and the filter is matched based on aggregate state only
            // an offset of 99 will match any X or Y position
            mods.Add("DELETE");
            mods.Add("""{"Id":"RustDeoxidizer","Input":false,"OffsetX":99,"OffsetY":99,"Filter":["Solid"]}""");
        }
    }

    public enum PostModState { None, Skip, Add, Delete }
}
