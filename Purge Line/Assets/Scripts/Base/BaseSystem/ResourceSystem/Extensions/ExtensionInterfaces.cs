// ============================================================================
// PurgeLine.Resource.Extensions — P3 预留扩展接口
// ============================================================================

using Cysharp.Threading.Tasks;

namespace PurgeLine.Resource.Extensions
{
    /// <summary>
    /// [P3] 资源解密接口。支持自定义资源加密方案。
    /// 实现后通过 ResourceManagerConfig 或 ResourceManager 构造注入。
    /// </summary>
    public interface IResourceDecryptor
    {
        /// <summary>解密资源字节流</summary>
        byte[] Decrypt(byte[] encryptedData, string address);
    }

    /// <summary>
    /// [P3] 资源变体选择器。根据画质/语言等条件选择对应的资源变体。
    /// </summary>
    public interface IVariantSelector
    {
        /// <summary>将原始 address 映射为当前变体的 address</summary>
        string SelectVariant(string originalAddress);
    }

    /// <summary>
    /// [P3] 热更新回调接口。与热更新流程对接。
    /// </summary>
    public interface IHotUpdateCallback
    {
        /// <summary>热更新开始前回调</summary>
        UniTask OnBeforeHotUpdate();

        /// <summary>热更新完成后回调（新资源已就绪）</summary>
        UniTask OnAfterHotUpdate();

        /// <summary>热更新失败回调</summary>
        void OnHotUpdateFailed(string error);
    }

    /// <summary>
    /// [P3] ECS 架构兼容接口。支持 Entities 环境下的资源加载。
    /// </summary>
    public interface IEcsResourceBridge
    {
        /// <summary>在 ECS World 中注册资源句柄的 Entity 映射</summary>
        void RegisterEntity(string address, int entityIndex);

        /// <summary>从 ECS Entity 获取资源地址</summary>
        string GetAddressFromEntity(int entityIndex);
    }
}

