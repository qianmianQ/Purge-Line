using Microsoft.Extensions.Logging;
using TowerDefense.Components;
using TowerDefense.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.Systems
{
    /// <summary>
    /// 网格渲染系统 — 批量渲染地图格子
    ///
    /// 使用 Graphics.RenderMeshInstanced 实现零 GameObject 的高性能渲染：
    /// - 每种格子类型一批 draw call
    /// - IJobParallelFor + Burst 填充 transform 矩阵
    /// - 主线程发起 GPU 绘制
    ///
    /// 性能目标：
    /// - 200×200 地图 < 2ms 渲染帧耗时
    /// - 0 GC allocation per frame
    ///
    /// 运行在 PresentationSystemGroup 中。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct GridRenderSystem : ISystem
    {
        private static ILogger _logger;

        /// <summary>每批最大实例数（Graphics.RenderMeshInstanced 限制）</summary>
        private const int MaxInstancesPerBatch = 1023;

        /// <summary>支持的格子渲染类型数量</summary>
        private const int RenderTypeCount = 4;

        // 渲染资源（持久分配，避免每帧 GC）
        private NativeList<Matrix4x4> _solidMatrices;
        private NativeList<Matrix4x4> _walkableMatrices;
        private NativeList<Matrix4x4> _placeableMatrices;
        private NativeList<Matrix4x4> _compositeMatrices;

        // 缓存是否初始化
        private bool _resourcesInitialized;

        public void OnCreate(ref SystemState state)
        {
            _logger = GameLogger.Create<GridRenderSystem>();
            state.RequireForUpdate<GridMapData>();

            _solidMatrices = new NativeList<Matrix4x4>(4096, Allocator.Persistent);
            _walkableMatrices = new NativeList<Matrix4x4>(4096, Allocator.Persistent);
            _placeableMatrices = new NativeList<Matrix4x4>(4096, Allocator.Persistent);
            _compositeMatrices = new NativeList<Matrix4x4>(4096, Allocator.Persistent);

            _resourcesInitialized = true;
            _logger.LogInformation("[GridRenderSystem] Created");
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_solidMatrices.IsCreated) _solidMatrices.Dispose();
            if (_walkableMatrices.IsCreated) _walkableMatrices.Dispose();
            if (_placeableMatrices.IsCreated) _placeableMatrices.Dispose();
            if (_compositeMatrices.IsCreated) _compositeMatrices.Dispose();

            _resourcesInitialized = false;
            _logger.LogInformation("[GridRenderSystem] Destroyed");
        }

        public void OnUpdate(ref SystemState state)
        {
            //暂时取消渲染系统
            return;
            if (!_resourcesInitialized) return;
            if (!SystemAPI.HasSingleton<GridMapData>()) return;

            var mapData = SystemAPI.GetSingleton<GridMapData>();
            if (!mapData.BlobData.IsCreated) return;

            // 获取渲染资源配置
            var renderConfig = GridRenderConfig.Instance;
            if (renderConfig == null) return;

            // 只在脏标记时重建矩阵
            var singletonEntity = SystemAPI.GetSingletonEntity<GridMapData>();
            bool isDirty = state.EntityManager.HasComponent<GridDirtyTag>(singletonEntity);

            if (isDirty)
            {
                RebuildMatrices(ref mapData);
                state.EntityManager.RemoveComponent<GridDirtyTag>(singletonEntity);
            }

            // 执行渲染
            RenderBatched(renderConfig);
        }

        /// <summary>
        /// 重建所有格子类型的 transform 矩阵
        /// </summary>
        private void RebuildMatrices(ref GridMapData mapData)
        {
            _solidMatrices.Clear();
            _walkableMatrices.Clear();
            _placeableMatrices.Clear();
            _compositeMatrices.Clear();

            ref var cells = ref mapData.BlobData.Value.Cells;
            int width = mapData.Width;
            int height = mapData.Height;
            float cellSize = mapData.CellSize;
            float2 origin = mapData.Origin;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    var cellType = (CellType)cells[index];
                    if (cellType == CellType.None) continue;

                    GridMath.GridToWorld(
                        new int2(x, y), origin, cellSize, out float2 worldCenter);

                    var matrix = Matrix4x4.TRS(
                        new Vector3(worldCenter.x, worldCenter.y, 0f),
                        Quaternion.identity,
                        new Vector3(cellSize, cellSize, 1f)
                    );

                    // 按类型分类（复合类型优先匹配）
                    if (cellType == CellType.WalkableAndPlaceable)
                    {
                        _compositeMatrices.Add(matrix);
                    }
                    else if (cellType.IsSolid())
                    {
                        _solidMatrices.Add(matrix);
                    }
                    else if (cellType.IsWalkable())
                    {
                        _walkableMatrices.Add(matrix);
                    }
                    else if (cellType.IsPlaceable())
                    {
                        _placeableMatrices.Add(matrix);
                    }
                }
            }
        }

        /// <summary>
        /// 分批渲染所有格子
        /// </summary>
        private void RenderBatched(GridRenderConfig config)
        {
            if (config.QuadMesh == null) return;

            RenderTypeMatrices(config.QuadMesh, config.SolidMaterial, _solidMatrices);
            RenderTypeMatrices(config.QuadMesh, config.WalkableMaterial, _walkableMatrices);
            RenderTypeMatrices(config.QuadMesh, config.PlaceableMaterial, _placeableMatrices);
            RenderTypeMatrices(config.QuadMesh, config.CompositeMaterial, _compositeMatrices);
        }

        /// <summary>
        /// 渲染单种类型的所有格子（分批发送）
        /// </summary>
        private static void RenderTypeMatrices(Mesh mesh, Material material,
            NativeList<Matrix4x4> matrices)
        {
            if (material == null || matrices.Length == 0) return;

            var renderParams = new RenderParams(material)
            {
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.Off,
                layer = 0
            };

            int remaining = matrices.Length;
            int offset = 0;

            while (remaining > 0)
            {
                int batchCount = math.min(remaining, MaxInstancesPerBatch);

                // 使用 NativeSlice 避免分配
                var slice = matrices.AsArray().GetSubArray(offset, batchCount);

                // // 需要转为托管数组才能调用 API（这里是渲染路径，可接受）
                // var managedArray = new Matrix4x4[batchCount];
                // NativeArray<Matrix4x4>.Copy(slice, managedArray, batchCount);
                //
                // Graphics.RenderMeshInstanced(renderParams, mesh, 0, managedArray);
                
                // --- 核心修改开始 ---
                // 不再使用 Instanced API，改为循环渲染每一个
                for (int i = 0; i < slice.Length; i++)
                {
                    // 注意：这里使用的是 Graphics.RenderMesh (单数)
                    Graphics.RenderMesh(renderParams, mesh, 0, slice[i]);
                }
                // --- 核心修改结束 ---

                offset += batchCount;
                remaining -= batchCount;
            }
        }
    }

    /// <summary>
    /// 网格渲染配置 — ScriptableObject Singleton
    ///
    /// 存储渲染所需的 Mesh 和 Material 引用。
    /// 在 Resources/GridRenderConfig 中放置一个实例。
    /// </summary>
    [CreateAssetMenu(fileName = "GridRenderConfig", menuName = "TowerDefense/Grid Render Config")]
    public class GridRenderConfig : ScriptableObject
    {
        private static GridRenderConfig _instance;

        public static GridRenderConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GridRenderConfig>("GridRenderConfig");
                }
                return _instance;
            }
        }

        [Header("Mesh")]
        [Tooltip("用于渲染格子的四边形 Mesh（1x1 单位）")]
        public Mesh QuadMesh;

        [Header("Materials（每种格子类型对应一个材质）")]
        public Material SolidMaterial;
        public Material WalkableMaterial;
        public Material PlaceableMaterial;
        public Material CompositeMaterial;

        /// <summary>
        /// 编辑器中自动创建默认 QuadMesh
        /// </summary>
        private void OnValidate()
        {
            if (QuadMesh == null)
            {
                QuadMesh = CreateDefaultQuad();
            }
        }

        /// <summary>
        /// 创建 1x1 单位的四边形 Mesh
        /// </summary>
        public static Mesh CreateDefaultQuad()
        {
            var mesh = new Mesh
            {
                name = "GridQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[]
                {
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back
                }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

