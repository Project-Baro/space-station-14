﻿using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Shared.Storage.Components
{
    [RegisterComponent]
    [Access(typeof(SharedItemMapperSystem))]
    public sealed class ItemMapperComponent : Component
    {
        [DataField("mapLayers")] public readonly Dictionary<string, SharedMapLayerData> MapLayers = new();

        [DataField("sprite")] public ResourcePath? RSIPath;

        public readonly List<string> SpriteLayers = new();
    }
}
