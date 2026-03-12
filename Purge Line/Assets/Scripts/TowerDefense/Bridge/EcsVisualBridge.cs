using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TowerDefense.Components.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.Bridge
{
    /// <summary>
    /// ECS ↔ GameObject 可视化桥接器
    ///
    /// ── 职责 ──────────────────────────────────────────────
    /// 1. 检测带有 VisualRequest 的 ECS 实体 → 实例化对应 Prefab
    /// 2. 每帧同步 ECS LocalTransform → GameObject Transform
    /// 3. 检测带有 DestroyTag / 已被 EntityManager 销毁的实体 → 回收 GameObject
    ///
    /// ── 使用方式 ──────────────────────────────────────────
    /// 场景中创建一个空 GameObject，挂载此脚本。
    /// 将 3 个 prefab 拖入对应字段：
    ///   • TowerPrefab  → Assets/Prefabs/Gameplay/Towers/Tower Entity.prefab
    ///   • EnemyPrefab  → Assets/Prefabs/Gameplay/Enemies/Enemy Entity.prefab
    ///   • BulletPrefab → Assets/Prefabs/Gameplay/Bullet.prefab
    ///
    /// ── 性能说明 ──────────────────────────────────────────
    /// 使用 EntityQuery + NativeArray 批量同步，避免逐实体 GetComponent。
    /// GameObject 层面使用简单对象池减少 Instantiate/Destroy 开销。
    /// </summary>
    public class EcsVisualBridge : MonoBehaviour
    {
        // ── Inspector 可配置 ──────────────────────────────────
        [Header("Prefab 配置（从 Assets/Prefabs 拖入）")]
        [Tooltip("Assets/Prefabs/Gameplay/Towers/Tower Entity.prefab")]
        public GameObject TowerPrefab;

        [Tooltip("Assets/Prefabs/Gameplay/Enemies/Enemy Entity.prefab")]
        public GameObject EnemyPrefab;

        [Tooltip("Assets/Prefabs/Gameplay/Bullet.prefab")]
        public GameObject BulletPrefab;

        // ── 内部数据 ──────────────────────────────────────────

        private static ILogger _logger;
        private World _ecsWorld;
        private EntityManager _em;
        private int _nextVisualId = 1;

        /// <summary>VisualId → 对应 GameObject</summary>
        private readonly Dictionary<int, GameObject> _visualMap = new(1024);

        /// <summary>Entity → VisualId（用于反向查询）</summary>
        private readonly Dictionary<Entity, int> _entityToVisual = new(1024);

        // ── 对象池 ────────────────────────────────────────────
        private readonly Queue<GameObject> _towerPool  = new(64);
        private readonly Queue<GameObject> _enemyPool  = new(512);
        private readonly Queue<GameObject> _bulletPool = new(512);

        // ── 缓存 Query ───────────────────────────────────────
        private EntityQuery _requestQuery;    // 有 VisualRequest 但没有 VisualLinked
        private EntityQuery _linkedQuery;     // 有 VisualLinked + LocalTransform
        private EntityQuery _destroyQuery;    // 有 VisualLinked + DestroyTag

        // ══════════════════════════════════════════════════════
        // Unity 生命周期
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            _logger = GameLogger.Create("EcsVisualBridge");

            _ecsWorld = World.DefaultGameObjectInjectionWorld;
            if (_ecsWorld == null || !_ecsWorld.IsCreated)
            {
                _logger.LogError("[EcsVisualBridge] ECS World not available!");
                enabled = false;
                return;
            }

            _em = _ecsWorld.EntityManager;

            // 构建 EntityQuery
            _requestQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<VisualRequest>(),
                ComponentType.Exclude<VisualLinked>());

            _linkedQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<VisualLinked>(),
                ComponentType.ReadOnly<LocalTransform>());

            _destroyQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<VisualLinked>(),
                ComponentType.ReadOnly<DestroyTag>());

            _logger.LogInformation("[EcsVisualBridge] Started. Tower={0} Enemy={1} Bullet={2}",
                TowerPrefab != null, EnemyPrefab != null, BulletPrefab != null);
        }

        private void LateUpdate()
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated) return;

            HandleNewRequests();
            SyncPositions();
            HandleDestroys();
        }

        private void OnDestroy()
        {
            // 销毁所有活跃 visual
            foreach (var go in _visualMap.Values)
            {
                if (go != null) Destroy(go);
            }
            _visualMap.Clear();
            _entityToVisual.Clear();

            // 清空池
            ClearPool(_towerPool);
            ClearPool(_enemyPool);
            ClearPool(_bulletPool);
        }

        // ══════════════════════════════════════════════════════
        // Step 1: 处理新的 VisualRequest
        // ══════════════════════════════════════════════════════

        private void HandleNewRequests()
        {
            if (_requestQuery.IsEmpty) return;

            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<VisualRequest>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                VisualType type = requests[i].Type;

                // 实例化 / 从池取
                GameObject go = GetOrCreateVisual(type);
                if (go == null)
                {
                    _logger.LogWarning("[EcsVisualBridge] No prefab for VisualType={0}", type);
                    // 移除请求，避免下帧重复报错
                    _em.RemoveComponent<VisualRequest>(entity);
                    continue;
                }

                // 分配 ID
                int visualId = _nextVisualId++;
                _visualMap[visualId] = go;
                _entityToVisual[entity] = visualId;

                // 初始位置
                if (_em.HasComponent<LocalTransform>(entity))
                {
                    var lt = _em.GetComponentData<LocalTransform>(entity);
                    go.transform.position = new Vector3(lt.Position.x, lt.Position.y, lt.Position.z);
                }

                go.SetActive(true);

                // 替换 VisualRequest → VisualLinked
                _em.RemoveComponent<VisualRequest>(entity);
                _em.AddComponentData(entity, new VisualLinked { VisualId = visualId });
            }

            entities.Dispose();
            requests.Dispose();
        }

        // ══════════════════════════════════════════════════════
        // Step 2: 同步 ECS Position → GameObject Position
        // ══════════════════════════════════════════════════════

        private void SyncPositions()
        {
            if (_linkedQuery.IsEmpty) return;

            var entities   = _linkedQuery.ToEntityArray(Allocator.Temp);
            var linked     = _linkedQuery.ToComponentDataArray<VisualLinked>(Allocator.Temp);
            var transforms = _linkedQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int visualId = linked[i].VisualId;
                if (_visualMap.TryGetValue(visualId, out var go) && go != null)
                {
                    var pos = transforms[i].Position;
                    go.transform.position = new Vector3(pos.x, pos.y, pos.z);
                }
            }

            entities.Dispose();
            linked.Dispose();
            transforms.Dispose();
        }

        // ══════════════════════════════════════════════════════
        // Step 3: 回收已销毁实体的 GameObject
        // ══════════════════════════════════════════════════════

        private void HandleDestroys()
        {
            // 3a. 有 DestroyTag 的实体 → 回收 GO
            if (!_destroyQuery.IsEmpty)
            {
                var entities = _destroyQuery.ToEntityArray(Allocator.Temp);
                var linked   = _destroyQuery.ToComponentDataArray<VisualLinked>(Allocator.Temp);

                for (int i = 0; i < entities.Length; i++)
                {
                    RecycleVisual(entities[i], linked[i].VisualId);
                }

                entities.Dispose();
                linked.Dispose();
            }

            // 3b. 检查 _entityToVisual 中的 Entity 是否还存在
            //     （处理被其他系统直接 DestroyEntity 的情况）
            var orphans = new List<Entity>(16);
            foreach (var kv in _entityToVisual)
            {
                if (!_em.Exists(kv.Key))
                {
                    orphans.Add(kv.Key);
                }
            }

            foreach (var entity in orphans)
            {
                if (_entityToVisual.TryGetValue(entity, out int visualId))
                {
                    RecycleVisual(entity, visualId);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 对象池
        // ══════════════════════════════════════════════════════

        private GameObject GetOrCreateVisual(VisualType type)
        {
            Queue<GameObject> pool;
            GameObject prefab;

            switch (type)
            {
                case VisualType.Tower:
                    pool = _towerPool;
                    prefab = TowerPrefab;
                    break;
                case VisualType.Enemy:
                    pool = _enemyPool;
                    prefab = EnemyPrefab;
                    break;
                case VisualType.Bullet:
                    pool = _bulletPool;
                    prefab = BulletPrefab;
                    break;
                default:
                    return null;
            }

            // 从池中取
            while (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled != null) return pooled;
            }

            // 池空 → 新建
            if (prefab == null)
            {
                _logger.LogWarning("[EcsVisualBridge] Prefab is null for type={0}, creating placeholder", type);
                return CreatePlaceholder(type);
            }

            var go = Instantiate(prefab, transform);
            go.name = $"[{type}_{_nextVisualId}]";
            return go;
        }

        /// <summary>
        /// 创建占位 GameObject（prefab 为 null 时的降级方案）
        /// </summary>
        private GameObject CreatePlaceholder(VisualType type)
        {
            var go = new GameObject($"[Placeholder_{type}_{_nextVisualId}]");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = type == VisualType.Bullet ? 10 : 5;

            // 1×1 白色像素 Sprite
            var tex = new Texture2D(1, 1);
            Color c = type switch
            {
                VisualType.Tower  => Color.blue,
                VisualType.Enemy  => Color.red,
                VisualType.Bullet => Color.yellow,
                _                 => Color.white
            };
            tex.SetPixel(0, 0, c);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 2f);

            return go;
        }

        private void RecycleVisual(Entity entity, int visualId)
        {
            _entityToVisual.Remove(entity);
            if (!_visualMap.TryGetValue(visualId, out var go))
                return;
            _visualMap.Remove(visualId);

            if (go == null) return;

            go.SetActive(false);

            // 判断类型放回对应池（通过名字前缀或 tag 判断）
            // 简单起见统一回收到 enemy 池……更好的做法是给 GO 挂一个 PoolTag 脚本
            // 这里通过名字前缀判断
            string n = go.name;
            if (n.StartsWith("[Tower") || n.StartsWith("[Placeholder_Tower"))
                _towerPool.Enqueue(go);
            else if (n.StartsWith("[Enemy") || n.StartsWith("[Placeholder_Enemy"))
                _enemyPool.Enqueue(go);
            else if (n.StartsWith("[Bullet") || n.StartsWith("[Placeholder_Bullet"))
                _bulletPool.Enqueue(go);
            else
                _enemyPool.Enqueue(go); // fallback
        }

        private static void ClearPool(Queue<GameObject> pool)
        {
            while (pool.Count > 0)
            {
                var go = pool.Dequeue();
                if (go != null) Destroy(go);
            }
        }
    }
}

