using UnityEngine;
using UnityEngine.UI;

public class Tip : MonoBehaviour
{
    [SerializeField] private GameSessionDataSO _gameSessionData;
    [SerializeField] private Image _singel;
    [SerializeField] private Image _muti;


    private void Start()
    {
        if (_gameSessionData.PlayerCount == 1)
        {
            _singel.gameObject.SetActive(true);
            _muti.gameObject.SetActive(false);
        }

        else if (_gameSessionData.PlayerCount == 2)
        {
            _singel.gameObject.SetActive(false);
            _muti.gameObject.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
    }
}
