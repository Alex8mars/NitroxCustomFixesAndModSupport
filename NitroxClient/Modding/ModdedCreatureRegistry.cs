using System;
using System.Collections.Generic;
using UnityEngine;

namespace NitroxClient.Modding;

/// <summary>
/// Registry of modded creature prefabs and sync hints so we can spawn them in multiplayer.
/// </summary>
public static class ModdedCreatureRegistry
{
    private static readonly Dictionary<string, ModdedCreatureRegistration> registrations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object sync = new();

    public static void Register(ModdedCreatureRegistration registration)
    {
        if (registration == null)
        {
            return;
        }

        lock (sync)
        {
            registrations[registration.ClassId] = registration;
        }

        Nitrox.Model.Logger.Log.Info($"Registered modded creature {registration.ClassId} for future sync support.");
    }

    public static bool TryGetPrefab(string classId, out GameObject prefab)
    {
        lock (sync)
        {
            if (registrations.TryGetValue(classId, out ModdedCreatureRegistration registration))
            {
                prefab = registration.GetPrefabInstance();
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public static ModdedCreatureSyncMetadata? GetSyncMetadata(string classId)
    {
        lock (sync)
        {
            if (registrations.TryGetValue(classId, out ModdedCreatureRegistration registration))
            {
                return registration.SyncMetadata;
            }
        }

        return null;
    }
}

public sealed class ModdedCreatureRegistration
{
    private readonly Func<GameObject> prefabFactory;

    public ModdedCreatureRegistration(string classId, Func<GameObject> prefabFactory, ModdedCreatureSyncMetadata? syncMetadata = null)
    {
        ClassId = classId;
        this.prefabFactory = prefabFactory;
        SyncMetadata = syncMetadata ?? new ModdedCreatureSyncMetadata(classId);
    }

    public string ClassId { get; }

    public ModdedCreatureSyncMetadata SyncMetadata { get; }

    public GameObject GetPrefabInstance()
    {
        GameObject prefab = prefabFactory?.Invoke();
        if (prefab == null)
        {
            return null;
        }

        return UnityEngine.Object.Instantiate(prefab);
    }
}

/// <summary>
/// Information about how the modded creature should be synchronized between clients.
/// </summary>
public sealed class ModdedCreatureSyncMetadata
{
    public ModdedCreatureSyncMetadata(string classId)
    {
        ClassId = classId;
    }

    /// <summary>
    /// Unique class id for the custom creature prefab.
    /// </summary>
    public string ClassId { get; }

    /// <summary>
    /// Optional serializer identifier for future custom state replication.
    /// </summary>
    public string? SerializerId { get; init; }

    /// <summary>
    /// Whether the creature should opt into Nitrox ownership sync once implemented.
    /// </summary>
    public bool ParticipatesInOwnership { get; init; } = true;
}

/// <summary>
/// Helper exposed in the BepInEx namespace so plugins can register creatures without referencing Nitrox assemblies directly.
/// </summary>
namespace BepInEx
{
    public static class NitroxIntegration
    {
        public static void RegisterModdedCreature(string classId, Func<GameObject> prefabFactory)
        {
            ModdedCreatureRegistry.Register(new ModdedCreatureRegistration(classId, prefabFactory));
        }

        public static void RegisterModdedCreature(ModdedCreatureRegistration registration)
        {
            ModdedCreatureRegistry.Register(registration);
        }
    }
}
