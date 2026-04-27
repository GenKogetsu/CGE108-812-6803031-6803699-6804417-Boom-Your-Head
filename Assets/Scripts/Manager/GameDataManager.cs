using Genoverrei.DesignPattern;
using UnityEngine;

public class GameDataManager : Singleton<GameDataManager>
{
    public GameSessionDataSO GameSessionData;
    public MapDataSO MapData;

    protected override void Awake()
    {
        base.Awake();

        GameSessionData.ResetScripts();
        MapData.ResetScripts();

        Cursor.lockState = CursorLockMode.None;
    }

    private void Start()
    {
        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReleaseAllPools();
    }


}
