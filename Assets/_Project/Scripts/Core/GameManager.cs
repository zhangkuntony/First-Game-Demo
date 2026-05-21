using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WastelandHaven.Core
{
    /// <summary>
    /// 游戏全局管理器 - 负责游戏生命周期、子模块初始化、状态管理
    /// 使用单例模式 + DontDestroyOnLoad 实现跨场景持久化
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 单例模式
        
        private static GameManager _instance;
        
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        _instance = go.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
        }
        
        #endregion

        #region 枚举定义
        
        /// <summary>
        /// 游戏状态枚举
        /// </summary>
        public enum GameState
        {
            MainMenu,       // 主菜单
            Playing,        // 游戏进行中
            Paused,         // 暂停中
            Loading,        // 加载中
            Transitioning   // 场景切换中
        }

        #endregion

        #region 公共属性
        
        [Header("当前状态")]
        [SerializeField] private GameState _currentState = GameState.MainMenu;
        
        /// <summary>
        /// 当前游戏状态
        /// </summary>
        public GameState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var previousState = _currentState;
                    _currentState = value;
                    
                    // 触发状态变化事件
                    OnStateChanged?.Invoke(previousState, value);
                    
                    Debug.Log($"[GameManager] 状态切换: {previousState} → {value}");
                }
            }
        }

        /// <summary>
        /// 是否处于可操作的游戏状态
        /// </summary>
        public bool IsGameActive => CurrentState == GameState.Playing || CurrentState == GameState.Paused;

        /// <summary>
        /// 当前存档数据（运行时使用）
        /// </summary>
        public SaveData CurrentSaveData { get; private set; }

        /// <summary>
        /// 总游玩时间（秒）
        /// </summary>
        public float TotalPlayTime { get; private set; }

        #endregion

        #region 子模块引用（懒加载）

        private ResourceManager _resourceManager;
        private TimeManager _timeManager;
        private SaveSystem _saveSystem;
        private ProductionManager _productionManager;

        /// <summary>
        /// 资源管理器
        /// </summary>
        public ResourceManager ResourceManager => 
            _resourceManager ??= GetOrCreateModule<ResourceManager>("ResourceManager");

        /// <summary>
        /// 时间管理器（真实时间系统）
        /// </summary>
        public TimeManager TimeManager => 
            _timeManager ??= GetOrCreateModule<TimeManager>("TimeManager");

        /// <summary>
        /// 存档系统
        /// </summary>
        public SaveSystem SaveSystem => 
            _saveSystem ??= GetComponentInChildren<SaveSystem>() ?? gameObject.AddComponent<SaveSystem>();

        /// <summary>
        /// 生产系统管理器
        /// </summary>
        public ProductionManager ProductionManager => 
            _productionManager ??= GetOrCreateModule<ProductionManager>("ProductionManager");

        #endregion

        #region 事件定义

        /// <summary>
        /// 游戏状态变化事件：参数(旧状态, 新状态)
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>
        /// 游戏初始化完成事件
        /// </summary>
        public event Action OnGameInitialized;

        /// <summary>
        /// 新游戏开始事件
        /// </summary>
        public event Action OnNewGameStarted;

        /// <summary>
        /// 游戏加载完成事件
        /// </summary>
        public event Action OnGameLoaded;

        /// <summary>
        /// 游戏保存完成事件
        /// </summary>
        public event Action OnGameSaved;

        /// <summary>
        /// 游戏退出前事件
        /// </summary>
        public event Action OnBeforeQuit;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 单例处理
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameManager] 检测到重复实例，销毁当前对象");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            
            // 跨场景持久化
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[GameManager] Awake - 初始化开始");
        }

        private void Start()
        {
            // 初始化所有子系统
            InitializeAllSystems();
            
            // 根据当前场景设置初始状态
            var currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu" || currentScene == "Main")
            {
                CurrentState = GameState.MainMenu;
            }
            else
            {
                CurrentState = GameState.Playing;
            }
            
            // 标记初始化完成
            OnGameInitialized?.Invoke();
            Debug.Log("[GameManager] 所有系统初始化完成");
        }

        private void Update()
        {
            // 更新总游玩时间
            if (CurrentState == GameState.Playing)
            {
                TotalPlayTime += Time.deltaTime;
            }
        }

        private void OnDestroy()
        {
            // 清理事件订阅
            CleanupEvents();
            
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && IsGameActive)
            {
                Debug.Log("[GameManager] 应用进入后台，自动保存");
                
                // 应用进入后台时自动保存
                AutoSave();
                
                // 记录离线时间
                TimeManager?.RecordOfflineTime();
            }
            else if (!pauseStatus && IsGameActive)
            {
                Debug.Log("[GameManager] 应用恢复前台");
                
                // 从后台恢复时计算离线产出
                TimeManager?.CalculateAndApplyOfflineProduction();
            }
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[GameManager] 游戏退出，执行清理");
            
            // 触发退出前事件
            OnBeforeQuit?.Invoke();
            
            // 最终保存
            if (IsGameActive)
            {
                AutoSave();
            }
        }

        #endregion

        #region 系统初始化

        /// <summary>
        /// 初始化所有子系统
        /// </summary>
        private void InitializeAllSystems()
        {
            Debug.Log("[GameManager] 正在初始化所有子系统...");
            
            // 1. 初始化时间管理器（必须第一个初始化，其他系统可能依赖时间计算）
            InitializeModule<TimeManager>(ref _timeManager);
            
            // 2. 初始化存档系统
            InitializeModule<SaveSystem>(ref _saveSystem);
            
            // 3. 初始化资源管理器
            InitializeModule<ResourceManager>(ref _resourceManager);
            
            // 4. 初始化生产系统
            InitializeModule<ProductionManager>(ref _productionManager);

            Debug.Log("[GameManager] 所有子系统初始化完成");
        }

        /// <summary>
        /// 初始化指定类型的模块
        /// </summary>
        private void InitializeModule<T>(ref T moduleRef) where T : Component
        {
            if (moduleRef != null) return;
            
            moduleRef = GetComponent<T>();
            if (moduleRef == null)
            {
                string moduleName = typeof(T).Name;
                GameObject moduleObj = new GameObject(moduleName);
                moduleObj.transform.SetParent(transform);
                moduleRef = moduleObj.AddComponent<T>();
            }
            
            Debug.Log($"[GameManager] ✓ 模块已就绪: {typeof(T).Name}");
        }

        /// <summary>
        /// 获取或创建指定类型的模块
        /// </summary>
        private T GetOrCreateModule<T>(string objectName) where T : Component
        {
            // 先查找是否已有
            var existing = GetComponentInChildren<T>();
            if (existing != null) return existing;
            
            // 创建新的
            var go = new GameObject(objectName);
            go.transform.SetParent(transform);
            return go.AddComponent<T>();
        }

        #endregion

        #region 游戏流程控制

        /// <summary>
        /// 开始新游戏
        /// </summary>
        public void StartNewGame(int saveSlot = 1)
        {
            Debug.Log($"[GameManager] 开始新游戏 (存档位: {saveSlot})");
            
            try
            {
                CurrentState = GameState.Transitioning;
                
                // 1. 创建全新的存档数据
                CurrentSaveData = SaveSystem.CreateNewSave(saveSlot);
                CurrentSaveData.totalPlayTime = 0;
                
                TotalPlayTime = 0f;
                
                // 2. 重置所有子系统到初始状态
                ResetAllSystems();
                
                // 3. 设置初始资源（新手礼包）
                GiveStarterResources();
                
                // 4. 加载主游戏场景（如果不在的话）
                LoadMainSceneIfNeeded();
                
                // 5. 更新状态为游戏中
                CurrentState = GameState.Playing;
                
                // 6. 触发事件
                OnNewGameStarted?.Invoke();
                
                Debug.Log("[GameManager] ✅ 新游戏创建成功！");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] ❌ 创建新游戏失败: {e.Message}\n{e.StackTrace}");
                CurrentState = GameState.MainMenu;
            }
        }

        /// <summary>
        /// 加载存档
        /// </summary>
        public bool LoadGame(int saveSlot = 1)
        {
            Debug.Log($"[GameManager] 加载存档 (存档位: {saveSlot})");
            
            try
            {
                CurrentState = GameState.Loading;
                
                // 1. 从存档系统读取数据
                var saveData = SaveSystem.LoadGame(saveSlot);
                if (saveData == null)
                {
                    Debug.LogError($"[GameManager] ❌ 存档位 {saveSlot} 无有效存档");
                    return false;
                }
                
                CurrentSaveData = saveData;
                TotalPlayTime = saveData.totalPlayTime;
                
                // 2. 恢复各子系统状态
                RestoreAllSystems(saveData);
                
                // 3. 加载主游戏场景
                LoadMainSceneIfNeeded();
                
                // 4. 更新状态
                CurrentState = GameState.Playing;
                
                // 5. 触发事件
                OnGameLoaded?.Invoke();
                
                Debug.Log($"[GameManager] ✅ 存档加载成功！(上次保存: {saveData.lastSaveTime})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] ❌ 加载存档失败: {e.Message}\n{e.StackTrace}");
                CurrentState = GameState.MainMenu;
                return false;
            }
        }

        /// <summary>
        /// 手动保存游戏
        /// </summary>
        public bool SaveGame()
        {
            if (!IsGameActive)
            {
                Debug.LogWarning("[GameManager] 无法在非活跃状态下保存");
                return false;
            }
            
            Debug.Log("[GameManager] 正在保存游戏...");
            
            try
            {
                // 1. 收集所有系统的最新状态
                CollectAllSystemStates();
                
                // 2. 更新存档元信息
                CurrentSaveData.lastSaveTime = DateTime.UtcNow;
                CurrentSaveData.totalPlayTime = TotalPlayTime;
                
                // 3. 执行保存
                bool success = SaveSystem.SaveGame(CurrentSaveData.saveSlotId, CurrentSaveData);
                
                if (success)
                {
                    Debug.Log($"[GameManager] ✅ 保存成功！时间: {CurrentSaveData.lastSaveTime}");
                    OnGameSaved?.Invoke();
                }
                else
                {
                    Debug.LogError("[GameManager] ❌ 保存失败！");
                }
                
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] ❌ 保存异常: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 自动保存（用于应用退到后台等场景）
        /// </summary>
        private void AutoSave()
        {
            // 静默保存，不触发UI通知
            try
            {
                CollectAllSystemStates();
                CurrentSaveData.lastSaveTime = DateTime.UtcNow;
                CurrentSaveData.totalPlayTime = TotalPlayTime;
                SaveSystem.SaveGame(CurrentSaveData.saveSlotId, CurrentSaveData, silent: true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] 自动保存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            
            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
            Debug.Log("[GameManager] ⏸️ 游戏暂停");
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            Debug.Log("[GameManager] ▶️ 游戏恢复");
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        public void ReturnToMainMenu()
        {
            Debug.Log("[GameManager] 返回主菜单");
            
            CurrentState = GameState.Transitioning;
            
            // 保存当前进度
            if (IsGameActive)
            {
                AutoSave();
            }
            
            Time.timeScale = 1f;  // 重置时间缩放
            
            // 切换场景
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[GameManager] 退出游戏");
            
            OnBeforeQuit?.Invoke();
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region 数据收集与恢复

        /// <summary>
        /// 收集所有系统的状态用于保存
        /// </summary>
        private void CollectAllSystemStates()
        {
            // 收集资源状态
            if (_resourceManager != null)
            {
                CurrentSaveData.resources = _resourceManager.GetResourceStatesForSave();
            }
            
            // 收集采集建筑状态
            // TODO: 当实现采集系统后添加
            // CurrentSaveData.harvestBuildings = harvestBuildingManager.GetStatesForSave();
            
            // 收集生产队列状态
            // TODO: 当实现生产系统后添加
            // CurrentSaveData.productionQueues = productionManager.GetStatesForSave();
        }

        /// <summary>
        /// 将所有系统重置为新游戏初始状态
        /// </summary>
        private void ResetAllSystems()
        {
            // 重置资源管理器
            _resourceManager?.ResetToDefault();
            
            // 重置时间管理器
            _timeManager?.Reset();
            
            // 其他系统...
        }

        /// <summary>
        /// 从存档数据恢复所有系统状态
        /// </summary>
        private void RestoreAllSystems(SaveData saveData)
        {
            // 恢复资源状态
            if (_resourceManager != null && saveData.resources != null)
            {
                _resourceManager.RestoreFromSaveData(saveData.resources);
            }
            
            // 恢复时间管理器的最后在线时间
            if (_timeManager != null)
            {
                _timeManager.SetLastOnlineTime(saveData.lastSaveTime);
            }
            
            // 恢复其他系统...
        }

        /// <summary>
        /// 给予新手初始资源
        /// </summary>
        private void GiveStarterResources()
        {
            if (_resourceManager == null) return;
            
            // 给予一些基础资源帮助玩家快速上手
            _resourceManager.AddResource(ResourceType.Crop, 50);      // 50个农作物
            _resourceManager.AddResource(ResourceType.Wood, 30);      // 30个木材
            _resourceManager.AddResource(ResourceType.Ore, 20);       // 20个矿石
            _resourceManager.AddResource(ResourceType.Water, 100);    // 100个水源
            
            Debug.Log("[GameManager] 已发放新手资源包 🎁");
        }

        #endregion

        #region 场景管理

        /// <summary>
        /// 如果需要则加载主游戏场景
        /// </summary>
        private void LoadMainSceneIfNeeded()
        {
            var currentSceneName = SceneManager.GetActiveScene().name;
            const string mainSceneName = "Main";
            
            if (currentSceneName != mainSceneName)
            {
                Debug.Log($"[GameManager] 加载主场景: {mainSceneName}");
                SceneManager.LoadScene(mainSceneName);
            }
        }

        #endregion

        #region 清理方法

        /// <summary>
        /// 清理所有事件订阅，防止内存泄漏
        /// </summary>
        private void CleanupEvents()
        {
            OnStateChanged = null;
            OnGameInitialized = null;
            OnNewGameStarted = null;
            OnGameLoaded = null;
            OnGameSaved = null;
            OnBeforeQuit = null;
        }

        #endregion

        #region 调试辅助

#if UNITY_EDITOR || DEVELOPMENT_BUILD

        /// <summary>
        /// 在Unity Inspector显示调试信息
        /// </summary>
        [Header("调试信息")]
        [SerializeField] private bool _showDebugInfo = true;

        void OnGUI()
        {
            if (!_showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 300, 400, 200));
            GUILayout.Box($"[GameManager 调试]");
            GUILayout.Label($"状态: {CurrentState}");
            GUILayout.Label($"游玩时长: {(int)TotalPlayTime}秒 ({TotalPlayTime / 3600f:F1}小时)");
            GUILayout.Label($"存档位: {(CurrentSaveData?.saveSlotId.ToString() ?? "无")}");
            GUILayout.Label($"上次保存: {(CurrentSaveData?.lastSaveTime.ToString("HH:mm:ss") ?? "无")}");
            GUILayout.EndArea();
        }

#endif

        #endregion
    }


    #region 配套的数据类（临时放在这里，后期应移至独立文件）

    /// <summary>
    /// 完整的存档数据结构
    /// 后期可以移到 SaveData.cs 文件中
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int saveSlotId;
        public DateTime lastSaveTime;
        public long totalPlayTime;  // 秒
        
        // 资源状态列表
        public List<ResourceState> resources;
        
        // 采集建筑状态（预留）
        public List<HarvestBuildingState> harvestBuildings;
        
        // 生产队列状态（预留）
        public List<ProductionQueueState> productionQueues;
    }

    [Serializable]
    public class ResourceState
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    public class HarvestBuildingState
    {
        public string buildingId;
        public float positionX, positionY, positionZ;
        public int level;
        public long lastHarvestTimestamp;  // Unix时间戳（毫秒）
    }

    [Serializable]
    public class ProductionQueueState
    {
        public string buildingId;
        public List<QueueItemState> queueItems;
    }

    [Serializable]
    public class QueueItemState
    {
        public string recipeId;
        public long startTimeTimestamp;
        public long endTimeTimestamp;
        public int status;  // 0=等待, 1=进行中, 2=已完成
    }

    #endregion


    #region 资源类型枚举（临时放置）- 后期移至 ResourceType.cs

    /// <summary>
    /// 资源类型枚举
    /// 包含三层资源体系的所有类型
    /// </summary>
    public enum ResourceType
    {
        // ========== 第一层：基础资源（自然采集获得） ==========
        
        Crop,           // 农作物 - 农田种植收获
        Wood,           // 木材 - 林地砍伐
        Ore,            // 矿石 - 矿山开采
        Water,          // 水源 - 水井/集雨
        ScrapParts,     // 废弃零件 - 废墟搜寻

        // ========== 第二层：加工材料（生产建筑制造） ==========
        
        RefinedMetal,       // 精炼金属板 - 熔炉车间
        ReinforcedWood,     // 加固木材 - 木工坊
        ElectronicChip,     // 电子芯片 - 电子工坊
        ChemicalReagent,    // 化学试剂 - 化验室
        BuildingStone,      // 建筑石材 - 石材厂

        // ========== 第三层：成品物资（最终产出） ==========
        
        MachineGunTower,   // 机枪塔 - 塔防部署用
        AmmoSupplyBox,     // 弹药补给箱 - 战斗消耗品
        TowerUpgradeKit,   // 升级套件 - 塔防升级用
        BuildingMaterial,  // 建筑材料包 - 主城建造用
        RepairKit,         // 修复工具包 - 战斗维修用
    }

    #endregion
}
