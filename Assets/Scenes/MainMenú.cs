using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneController.Instance.LoadGameScene();
    }

    public void QuitGame()
    {
        SceneController.Instance.QuitGame();
    }
}