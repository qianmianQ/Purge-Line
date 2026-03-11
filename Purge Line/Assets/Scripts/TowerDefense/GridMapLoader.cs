using TowerDefense.Components;
using TowerDefense.Data;
using Unity.Entities;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 地图加载器 — MonoBehaviour 入口
    ///
    /// 挂在场景 GameObject 上，负责：
    /// 1. 从 Resources 加载关卡配置
    /// 2. 通过 ECS GridSpawnRequest 触发地图构建
    ///
    /// 前置条件：
    /// - Assets/Resources/Levels/{levelId}.bytes 文件存在（编辑器 Export to Resources）
    /// - Assets/Resources/GridRenderConfig.asset 存在并配置材质
    /// </summary>
    public class GridMapLoader : MonoBehaviour
    {
        [Tooltip("要加载的关卡 ID（与 LevelConfigAsset.levelId 一致）")]
        [SerializeField] private string levelId = "level_01";

        private void Start()
        {
            LoadMap(levelId);
        }

        /// <summary>
        /// 加载指定关卡并提交 ECS 生成请求
        /// </summary>
        public void LoadMap(string id)
        {
            // 1. 加载配置
            var config = LevelConfigLoader.LoadFromResources(id);
            if (config == null)
            {
                Debug.LogError($"[GridMapLoader] 关卡文件未找到: Resources/Levels/{id}.bytes\n" +
                               "请在编辑器中选中 LevelConfigAsset → Inspector → Export to Resources");
                return;
            }

            // 2. 存入共享仓库，拿到整数 ID（ECS 不能直接持有托管对象）
            int dataId = SharedLevelDataStore.Store(config);

            // 3. 获取默认 ECS World
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[GridMapLoader] ECS World 未初始化，请确认场景中有 SubScene 或 ECS 已启用");
                return;
            }

            // 4. 创建 GridSpawnRequest entity，GridSpawnSystem 会在下一帧处理它
            var em = world.EntityManager;
            var requestEntity = em.CreateEntity();
            em.AddComponentData(requestEntity, new GridSpawnRequest
            {
                Width    = config.Width,
                Height   = config.Height,
                CellSize = config.CellSize,
                OriginX  = config.OriginX,
                OriginY  = config.OriginY,
                CellDataId = dataId
            });

            Debug.Log($"[GridMapLoader] 已发送生成请求: {id} ({config.Width}×{config.Height}), CellSize={config.CellSize}");
        }
    }
}
