using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class AnimationController : MonoBehaviour , IPointerEnterHandler, IPointerExitHandler
{
    public Animator Animator;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Animator.SetBool("IsHovering", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Animator.SetBool("IsHovering", false);
    }

    public void OnClick()
    {
        Animator.SetTrigger("OnClick");
    }

    public void OnOuit(InputAction.CallbackContext context)
    {
        Animator.SetTrigger("OnOuit");
    }

}
