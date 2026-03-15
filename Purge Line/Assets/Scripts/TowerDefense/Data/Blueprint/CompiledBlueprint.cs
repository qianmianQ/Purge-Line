using MemoryPack;

namespace TowerDefense.Data.Blueprint
{
    // 编译后的蓝图（由 BlueprintCompiler 在 Editor 中生成），序列化为 .bytes 文件
    // 运行时通过 BlueprintRegistry 加载，启动时解析一次 TypeIndex，之后全走索引
    //
    // 架构限制（以下功能架构不支持，不在此处实现，需单独处理）：
    //   - IBufferElementData：DynamicBuffer 不是 blittable 值类型，无法 memcpy，需单独处理
    //   - BlobAssetReference：rawBytes 无法携带 Blob 引用，Warmup 后需单独写入
    //   - 含 managed 字段的组件（string、class 引用）：非 blittable，需转为 FixedString 或单独处理
    //   - ISharedComponentData：需要特殊 EntityManager API，不走此路径
    [MemoryPackable]
    public partial class CompiledBlueprint
    {
        // 当编译器输出格式发生破坏性变更时递增此值
        // 运行时 BlueprintRegistry 检测 version 不匹配时拒绝加载，提示重新编译
        public const int CurrentVersion = 1;

        public string blueprintId;
        // 源蓝图文档 hash（由编译器写入），用于增量编译和有效性校验。
        public string blueprintHash;
        public int version;
        public CompiledComponentRecord[] records;
    }

    // 单个组件的编译记录
    [MemoryPackable]
    public partial class CompiledComponentRecord
    {
        // AssemblyQualifiedName：TypeIndex 是运行时动态分配的，不可跨构建持久化
        // 此处存类型全名，运行时启动时 Type.GetType() 解析一次
        public string typeName;

        // blittable struct 的原始内存字节（通过 Marshal.Copy / UnsafeUtility.MemCpy 填充）
        // Tag 组件（零大小，isTagComponent == true）此字段为 null，Warmup 时跳过 MemCpy
        public byte[] rawBytes;

        // 是否为 Tag 组件（无实例字段，DOTS 中零大小）
        // Tag 组件只需要 AddComponent（已通过 Archetype 包含），无需写入组件数据
        // 零长度 rawBytes 的 memcpy 虽无崩溃风险，但属冗余操作，通过此标志统一跳过
        public bool isTagComponent;
    }
}
