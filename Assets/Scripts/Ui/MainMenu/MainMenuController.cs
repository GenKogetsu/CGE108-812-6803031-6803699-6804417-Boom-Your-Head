using Genoverrei.DesignPattern;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class MainMenuController : MonoBehaviour
{
    [Header("Data Reference")]
    [SerializeField] private GameSessionDataSO _sessionData; // 🚀 ลาก SO ใส่ตรงนี้

    [ReadOnly]
    [SerializeField] private Animator _animator;

    private bool _isStarting;
    private bool _isInMainMenu;

    public float _waitTime = 2f;

    private void OnValidate()
    {
        if (_animator == null) _animator = this.GetComponent<Animator>();
    }

    public void OnPressAnyKey(InputAction.CallbackContext context)
    {
        if (!context.started || _isStarting || _isInMainMenu) return;
        _isInMainMenu = true;
        ChangedState("ToMainMenu");
    }

    public void OnESC(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        ChangedState("ToMainMenu");
    }


    public void OnClickStartButton() => ChangedState("ToPlayeMode");
    public void OnClickCreditButton() => ChangedState("ToCredit");
    public void OnClickOptionButton() => ChangedState("ToOption");



    public void OnClickExitButton()
    {
        if (_isStarting) return;
        SceneEffectController.Instance.QuitGameAfterPlayEffect();
    }

    // 🚀 เล่นคนเดียว: เซ็ตค่าเป็น 1
    public void OnClickSiglePlayer()
    {
        if (_sessionData != null) _sessionData.PlayerCount = 1;
        if (!_isStarting) StartCoroutine(StartGameTransition());
    }

    // 🚀 เล่นสองคน: เซ็ตค่าเป็น 2
    public void OnClickMultiPlayer()
    {
        if (_sessionData != null) _sessionData.PlayerCount = 2;
        if (!_isStarting) StartCoroutine(StartGameTransition());
    }

    private IEnumerator StartGameTransition()
    {
        if (_isStarting) yield break;
        _isStarting = true;
        ResetAllBools();

        yield return new WaitForSeconds(_waitTime);
        SceneEffectController.Instance.LoadSceneAndPlayEffect("IQ Bot");
    }

    private void ChangedState(string state)
    {
        if (SceneEffectController.Instance.HaveSceneEffectCoroutine || _isStarting) return;
        ResetAllBools();
        if (state == "ToMainMenu") _animator.SetBool("ToMainMenu", true);
        if (state == "ToPlayeMode") _animator.SetBool("ToPlayeMode", true);
        if (state == "ToCredit") _animator.SetBool("ToCredit", true);
        if (state == "ToOption") _animator.SetBool("ToOption", true);
    }

    private void ResetAllBools()
    {
        _animator.SetBool("ToMainMenu", false);
        _animator.SetBool("ToPlayeMode", false);
        _animator.SetBool("ToCredit", false);
        _animator.SetBool("ToOption", false);
    }
}