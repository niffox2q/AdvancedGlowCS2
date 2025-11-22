using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using AdvancedGlow.Models;

namespace AdvancedGlow.Services;

public class GlowEntityService
{
    private GlowConfig _config;
    private readonly Dictionary<int, GlowEntitySet> _glowingEntities = new();
    private readonly object _lock = new();

    public GlowEntityService(GlowConfig config)
    {
        _config = config;
    }

    public void UpdateConfig(GlowConfig newConfig)
    {
        _config = newConfig;
    }

    public void CreateGlowForPlayer(CCSPlayerController player)
    {
        try
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) return;

            // Защита от ранних вызовов: если Pawn ещё не инициализирован по сцене/скелету — выйдем.
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn == null || !playerPawn.IsValid) return;

            var cbody = playerPawn.CBodyComponent;
            var sceneNode = cbody?.SceneNode;
            var skeleton = sceneNode?.GetSkeletonInstance();
            var modelName = skeleton?.ModelState?.ModelName ?? "";

            if (string.IsNullOrEmpty(modelName))
                return; // модель ещё не готова — не создаём сущности

            // Перед созданием — удалим старое если есть
            RemoveGlowFromPlayer(player);

            var glowRelay = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
            var glowEntity = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");

            if (glowRelay == null || glowEntity == null) return;

            // Настраиваем модели и флаги
            glowRelay.SetModel(modelName);
            glowRelay.RenderMode = RenderMode_t.kRenderNone;
            glowRelay.Spawnflags = 256u;
            glowRelay.DispatchSpawn();

            glowEntity.SetModel(modelName);
            glowEntity.Spawnflags = 256u;
            glowEntity.DispatchSpawn();

            // Настройка свечения
            glowEntity.Glow.GlowType = (byte)_config.GlowSettings.GlowStyle;
            glowEntity.Glow.GlowRange = _config.GlowSettings.GlowRange;
            glowEntity.Glow.GlowTeam = -1;

            lock (_lock)
            {
                _glowingEntities[player.Slot] = new GlowEntitySet(glowRelay, glowEntity);
            }
        }
        catch (Exception ex)
        {
            // Логируем, но не даём упасть плагину/серверу
            Console.WriteLine($"[AdvancedGlow] Ошибка CreateGlowForPlayer: {ex}");
        }
    }


    public void RemoveGlowFromPlayer(CCSPlayerController player)
    {
        if (player == null) return;

        lock (_lock)
        {
            if (_glowingEntities.Remove(player.Slot, out var entities))
            {
                if (entities.GlowRelay.IsValid) entities.GlowRelay.Remove();
                if (entities.GlowEntity.IsValid) entities.GlowEntity.Remove();
            }
        }
    }

    public void RemoveAllGlow()
    {
        lock (_lock)
        {
            foreach (var entities in _glowingEntities.Values)
            {
                if (entities.GlowRelay.IsValid) entities.GlowRelay.Remove();
                if (entities.GlowEntity.IsValid) entities.GlowEntity.Remove();
            }
            _glowingEntities.Clear();
        }
    }

    public HashSet<uint> GetAllGlowEntityIndices()
    {
        lock (_lock)
        {
            return _glowingEntities.Values
                .Where(set => set.GlowEntity.IsValid && set.GlowRelay.IsValid)
                .SelectMany(set => new[] { set.GlowEntity.Index, set.GlowRelay.Index })
                .ToHashSet();
        }
    }

    public GlowEntitySet? GetGlowEntities(CCSPlayerController player)
    {
        lock (_lock)
        {
            _glowingEntities.TryGetValue(player.Slot, out var entities);
            return entities;
        }
    }

    public List<CCSPlayerController> GetGlowingPlayers()
    {
        var players = new List<CCSPlayerController>();
        lock (_lock)
        {
            foreach (var slot in _glowingEntities.Keys)
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player != null && player.IsValid && player.PawnIsAlive)
                {
                    players.Add(player);
                }
            }
        }
        return players;
    }
}