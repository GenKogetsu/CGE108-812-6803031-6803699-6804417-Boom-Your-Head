using UnityEngine;
using UnityEngine.InputSystem;
using NaughtyAttributes;

public sealed class InputMapSwitcher : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Session Reference")]
    [Required]
    [SerializeField] private GameSessionDataSO _sessionData;

    [Header("Map Names")]
    [SerializeField] private string _singlePlayerMapName = "SinglePlayer";
    [SerializeField] private string _multiplayerMapName = "Multiplayer";

    private InputActionMap _singlePlayerMap;
    private InputActionMap _multiplayerMap;

    private void Awake()
    {
        if (_inputActions == null)
        {
            Debug.LogError("InputActionAsset ไม่ได้ถูกตั้งค่า");
            return;
        }

        _singlePlayerMap = _inputActions.FindActionMap(_singlePlayerMapName, true);
        _multiplayerMap = _inputActions.FindActionMap(_multiplayerMapName, true);

        DisableAll();      // สำคัญ
        ApplyPreset();

    }

    public void ApplyPreset()
    {
        if (_sessionData == null) return;

        DisableAll();      // ปิดทุก map ก่อน

        bool isSingle = _sessionData.PlayerCount == 1;

        if (isSingle)
        {
            _singlePlayerMap.Enable();
        }
        else
        {
            _multiplayerMap.Enable();
        }

        Debug.Log($"[InputMapSwitcher] Active Map : {(isSingle ? "SinglePlayer" : "Multiplayer")}");
    }

    public void DisableAll()
    {
        _singlePlayerMap?.Disable();
        _multiplayerMap?.Disable();
    }
}