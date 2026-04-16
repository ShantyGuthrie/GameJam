using UnityEngine;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    public PlayerController player;
    public TextMeshProUGUI textoVida;

    void Update()
    {
        if (player == null) return;

        textoVida.text = "LUMEN: " + ObtenerVida();
    }

    int ObtenerVida()
    {
        // 🔥 truco: sacamos vida desde el script
        return player.GetVidas();
    }
}