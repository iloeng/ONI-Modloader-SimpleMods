using Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HarmonyLib.Code;

namespace PipedEverything
{
    public class PipeConfig
    {
        public string? Id;
        public bool Input;
        public int OffsetX;
        public int OffsetY;
        public string[] Filter = [];

        public Color32? Color;
        public Color32? ColorBackground;
        public Color32? ColorBorder;
        public int? StorageIndex;
        public float? StorageCapacity;
        public bool? RemoveMaxAtmosphere;
        public Port? OriginalPort;

        [JsonIgnore] private ConduitType? _ConduitType;
        [JsonIgnore]
        public ConduitType ConduitType => _ConduitType ??= Filter.FirstOrDefault() switch
        {
            null => ConduitType.None,
            "Gas" => ConduitType.Gas,
            "Liquid" => ConduitType.Liquid,
            "Solid" => ConduitType.Solid,
            "DESTROY" => ConduitType.MAX,
            _ => Filter[0].ToElement().GetConduitType(),
        };

        public PipeConfig() { }

        public PipeConfig(string id, bool input, int x, int y, params string[] filter)
        {
            this.Id = id;
            this.Input = input;
            this.OffsetX = x;
            this.OffsetY = y;
            this.Filter = filter;
        }

        public PipeConfig(string id, bool input, int x, int y, params SimHashes[] filter)
        {
            this.Id = id;
            this.Input = input;
            this.OffsetX = x;
            this.OffsetY = y;
            this.Filter = filter.Select(s => s.ToString()).ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is not PipeConfig other) return false;
            if (this.Id != other.Id) return false;
            if (this.Input != other.Input) return false;
            if (this.OffsetX != other.OffsetX && this.OffsetX != 99 && other.OffsetX != 99) return false;
            if (this.OffsetY != other.OffsetY && this.OffsetY != 99 && other.OffsetY != 99) return false;
            if (this.ConduitType != other.ConduitType) return false;
            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = -1173585561;
            hashCode = hashCode * -1521134295 + (this.Id ?? "").GetHashCode();
            hashCode = hashCode * -1521134295 + this.Input.GetHashCode();
            hashCode = hashCode * -1521134295 + this.OffsetX.GetHashCode();
            hashCode = hashCode * -1521134295 + this.OffsetY.GetHashCode();
            hashCode = hashCode * -1521134295 + this.ConduitType.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(PipeConfig c1, PipeConfig c2) => c1.Equals(c2);
        public static bool operator !=(PipeConfig c1, PipeConfig c2) => !c1.Equals(c2);
    }

    public enum Port
    {
        Extra1,
        Extra2,
        Extra3,
        Extra4,
        Utility = 100,
    }
}
