using UnityEngine;
using UnityEngine.InputSystem;
using Genoverrei.DesignPattern;
using NaughtyAttributes;

public sealed class InputManager : MonoBehaviour, IPauseWhenSceneAnimation
{
    #region Variables

    [Header("Bot Communication")]
    [SerializeField] private BotInputChannelSO _botInputChannel;

    [Header("Session Reference")]
    [Required][SerializeField] private GameSessionDataSO _sessionData;

    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Map Names")]
    [SerializeField] private string _singlePlayerMapName = "SinglePlayer";
    [SerializeField] private string _multiplayerMapName = "Multiplayer";

    private InputActionMap _singlePlayerMap;
    private InputActionMap _multiplayerMap;

    #endregion

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (_inputActions == null)
        {
            Debug.LogError("InputActionAsset ไม่ได้ถูกตั้งค่าใน Inspector");
            return;
        }

        _singlePlayerMap = _inputActions.FindActionMap(_singlePlayerMapName, true);
        _multiplayerMap = _inputActions.FindActionMap(_multiplayerMapName, true);

        ApplyInputPreset();
    }

    private void ApplyInputPreset()
    {
        if (_sessionData == null) return;

        bool isSingle = _sessionData.PlayerCount == 1;

        if (isSingle)
        {
            _multiplayerMap?.Disable();
            _singlePlayerMap?.Enable();
        }
        else
        {
            _singlePlayerMap?.Disable();
            _multiplayerMap?.Enable();
        }

        Debug.Log($"<color=#4FC3F7>[InputManager]</color> Active Map : {(isSingle ? "SinglePlayer" : "Multiplayer")}");
    }

    #region Event & Lifecycle

    private void OnEnable()
    {
        if (_botInputChannel != null)
            _botInputChannel.OnBotActionTriggered += ExecuteHandleBotInput;

        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe<LoadSceneEvent>(OnSceneLoading);
    }

    private void OnDisable()
    {
        if (_botInputChannel != null)
            _botInputChannel.OnBotActionTriggered -= ExecuteHandleBotInput;

        if (EventBus.Instance != null)
            EventBus.Instance.Unsubscribe<LoadSceneEvent>(OnSceneLoading);

        _singlePlayerMap?.Disable();
        _multiplayerMap?.Disable();
    }

    #endregion

    #region Input Callbacks

    public void OnMoveP1(InputAction.CallbackContext context)
        => BroadcastAction(GetCharacterFromSession(0), ActionType.Move, new MoveInputEvent(context.ReadValue<Vector2>()));

    public void OnPlaceBombP1(InputAction.CallbackContext context)
    {
        if (context.performed)
            BroadcastAction(GetCharacterFromSession(0), ActionType.PlaceBomb, null);
    }

    public void OnMoveP2(InputAction.CallbackContext context)
        => BroadcastAction(GetCharacterFromSession(1), ActionType.Move, new MoveInputEvent(context.ReadValue<Vector2>()));

    public void OnPlaceBombP2(InputAction.CallbackContext context)
    {
        if (context.performed)
            BroadcastAction(GetCharacterFromSession(1), ActionType.PlaceBomb, null);
    }

    #endregion

    #region Private Logic

    private Character GetCharacterFromSession(int index)
    {
        if (_sessionData == null || _sessionData.SelectedPlayers == null)
            return Character.None;

        if (index >= _sessionData.SelectedPlayers.Count)
            return Character.None;

        return _sessionData.SelectedPlayers[index];
    }

    private void ExecuteHandleBotInput(Character target, ActionType action, IEvent subEvent)
        => BroadcastAction(target, action, subEvent);

    private void BroadcastAction(Character target, ActionType actionType, IEvent subEvent)
    {
        if (target == Character.None) return;

        var signal = new CharacterAction(target, actionType, subEvent);

        if (EventBus.Instance != null)
            EventBus.Instance.Publish<ISignal>(signal);
    }

    public void OnSceneLoading(LoadSceneEvent eventData)
    {
        if (eventData.Isloding)
        {
            _singlePlayerMap?.Disable();
            _multiplayerMap?.Disable();
        }
        else
        {
            ApplyInputPreset();
        }
    }

    #endregion
}