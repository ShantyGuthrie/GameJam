using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelOpciones;

    public void StartGame()
    {
        SceneManager.LoadScene("RelatoInicio");
    }

    public void Options()
    {
        panelOpciones.SetActive(true);
    }

    public void CerrarOpciones()
    {
        panelOpciones.SetActive(false);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}